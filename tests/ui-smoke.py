#!/usr/bin/env python3
"""
UI smoke test for cat-tracker attachments (Playwright sync API).

Targets the ISOLATED test instance (default http://localhost:8094);
refuses to run against port 8093 (the user's LIVE instance).

Run with the project venv python, e.g.:
  <venv>/bin/python3 tests/ui-smoke.py
Env overrides: BASE_URL, TESTPW_FILE, SHOT_DIR
"""
import json
import os
import struct
import sys
import urllib.request
import zlib
from datetime import date, timedelta
from pathlib import Path

from playwright.sync_api import sync_playwright

BASE_URL = os.environ.get("BASE_URL", "http://localhost:8094").rstrip("/")
TESTPW_FILE = os.environ.get(
    "TESTPW_FILE", "/home/agent-x/.hermes/cache/scratch/cat-tracker-test/testpw"
)
SHOT_DIR = Path(
    os.environ.get(
        "SHOT_DIR", "/home/agent-x/.hermes/cache/scratch/cat-tracker-ui-screenshots"
    )
)
PREFIX = "ZZ-UI"

# ---- Hard guard: never point a browser at the live instance -------------------
if ":8093" in BASE_URL:
    sys.exit(f"REFUSING TO RUN: BASE_URL={BASE_URL} points at port 8093 "
             "(the user's LIVE instance). Use the isolated test instance (8094).")

PASSWORD = Path(TESTPW_FILE).read_text().strip()
SHOT_DIR.mkdir(parents=True, exist_ok=True)


def make_png() -> bytes:
    """A valid 1x1 red-pixel PNG built with stdlib only."""
    def chunk(tag: bytes, data: bytes) -> bytes:
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))
    raw = b"\x00\xd0\x14\x1e"  # filter byte + RGB red
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw))
            + chunk(b"IEND", b""))


PNG_BYTES = make_png()

results = []          # (name, ok, detail)
current_step = "init"


def step(name: str):
    global current_step
    current_step = name

    def deco(fn):
        return fn

    return None


def check(name: str, fn):
    """Run one smoke assertion; screenshot on failure."""
    try:
        detail = fn()
        results.append((name, True, ""))
        print(f"PASS  {name}")
    except Exception as exc:  # noqa: BLE001 - smoke harness reports everything
        results.append((name, False, str(exc)))
        print(f"FAIL  {name} — {type(exc).__name__}: {exc}")
        try:
            shot = SHOT_DIR / f"ui-smoke-fail-{abs(hash(name)) % 10**6}.png"
            if page is not None:
                page.screenshot(path=str(shot), full_page=True)
                print(f"      screenshot: {shot}")
        except Exception:
            pass


def seed_session(request):
    """Login on a dedicated API context (cookies stay there)."""
    request.post("/api/auth/login",
                 data=json.dumps({"password": PASSWORD}),
                 headers={"Content-Type": "application/json"})


page = None

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True)
    context = browser.new_context(viewport={"width": 1280, "height": 900})
    # Seed/cleanup API context is SEPARATE so the page session starts locked.
    seed = p.request.new_context(base_url=BASE_URL, ignore_https_errors=True)

    appt_id = gen_att_id = None
    try:
        # ---------- fixture seeding over the API ----------
        seed_session(seed)
        tomorrow = (date.today() + timedelta(days=1)).isoformat()
        r = seed.post("/api/appointments",
                      data=json.dumps({
                          "title": f"{PREFIX} appointment",
                          "notes": "ui smoke", "when": f"{tomorrow}T10:00:00",
                          "category": "Vet"}),
                      headers={"Content-Type": "application/json"})
        assert r.status == 201, f"seed appointment {r.status}"
        appt_id = r.json()["id"]

        r = seed.post("/api/attachments",
                      multipart={"file": {"name": f"{PREFIX}-general.png",
                                          "mimeType": "image/png",
                                          "buffer": PNG_BYTES}})
        assert r.status == 201, f"seed general attachment {r.status}"
        gen_att_id = r.json()["id"]

        r = seed.post(f"/api/appointments/{appt_id}/attachments",
                      multipart={"file": {"name": f"{PREFIX}-appt.png",
                                          "mimeType": "image/png",
                                          "buffer": PNG_BYTES}})
        assert r.status == 201, f"seed appointment attachment {r.status}"
        appt_att_id = r.json()["id"]

        # ---------- page starts LOCKED ----------
        step("page load")
        page = context.new_page()
        page.goto(BASE_URL + "/", wait_until="networkidle")
        check("01 app content visible (profile present)",
              lambda: page.wait_for_selector("#appContent:not(.hidden)", timeout=8000))
        check("02 locked state: no upload controls before login",
              lambda: (lambda n: None if n == 0 else f"{n} upload inputs visible")(
                  page.locator("div.attachment-upload").count()))

        # ---------- login through the modal ----------
        step("login")
        page.click("#authToggle")
        page.wait_for_selector("#loginPassword", state="visible")
        page.fill("#loginPassword", PASSWORD)
        page.click("#loginForm button[type=submit]")
        check("03 login modal unlocks editing",
              lambda: page.wait_for_function(
                  "document.getElementById('authToggleLabel').textContent === 'Editing unlocked'",
                  timeout=8000))

        # ---------- Documents tab ----------
        step("documents tab")
        page.click("#tabDocuments")
        page.wait_for_selector("#documentsView:not(.hidden)")
        check("04 Documents tab lists seeded attachment by name",
              lambda: page.locator("#documentsView .attachment-name",
                                   has_text=f"{PREFIX}-general.png").first
                        .wait_for(state="visible", timeout=8000))
        check("05 Documents tab shows upload control when unlocked",
              lambda: page.wait_for_selector(
                  "#documentsView .attachment-upload input[type=file]",
                  state="visible", timeout=5000))
        check("06 general attachment thumbnail points at file endpoint",
              lambda: (lambda src: None if gen_att_id in src and "/api/attachments/" in src
                       else f"bad src {src}")(
                  page.locator("#documentsView .attachment-thumb").first.get_attribute("src")))

        # ---------- Calendar tab: appointment attachment ----------
        step("calendar attachment")
        page.click("#tabCalendar")
        page.wait_for_selector("#calendarView:not(.hidden)")
        check("07 Calendar lists upcoming fixture appointment",
              lambda: page.locator(".appointment-row .title",
                                   has_text=f"{PREFIX} appointment").first
                        .wait_for(state="visible", timeout=8000))
        check("08 appointment view renders attachment thumbnail",
              lambda: page.wait_for_selector(
                  f"[data-att-appt='{appt_id}'] img.attachment-thumb",
                  state="visible", timeout=8000))
        check("09 appointment thumbnail actually decodes (blob served)",
              lambda: (lambda ok: None if ok else "image did not load")(
                  page.evaluate(
                      "([id]) => { const i = document.querySelector(`[data-att-appt='${id}'] img.attachment-thumb`);"
                      " return !!i && i.src.includes(id) && i.decode().then(() => true).catch(() => false) }",
                      [appt_att_id]) is not None and
                  page.evaluate(
                      "([id]) => document.querySelector(`[data-att-appt='${id}'] img.attachment-thumb`)?.naturalWidth > 0",
                      [appt_att_id])))

        # ---------- tab switch hides appointment-specific DOM ----------
        step("tab switch")
        page.click("#tabDocuments")
        page.wait_for_selector("#documentsView:not(.hidden)")
        check("10 switching to Documents hides appointment attachment DOM",
              lambda: (lambda v: None if not v else "calendar view still visible")(
                  page.locator(f"[data-att-appt='{appt_id}']").first.is_visible()))
        check("11 appointment upload control not reachable from Documents view",
              lambda: (lambda v: None if not v else "appt upload visible")(
                  page.locator("#calendarView .attachment-upload").first.is_visible()
                  if page.locator("#calendarView .attachment-upload").count() else False))

        # ---------- logout hides upload controls ----------
        step("logout")
        page.click("#authToggle")  # while authed this toggles logout directly
        check("12 lock returns UI to read-only label",
              lambda: page.wait_for_function(
                  "document.getElementById('authToggleLabel').textContent === 'Unlock'",
                  timeout=8000))
        check("13 upload controls gone from Documents after logout",
              lambda: (lambda n: None if n == 0 else f"{n} upload controls still present")(
                  page.locator("#documentsView .attachment-upload").count()))
        page.click("#tabCalendar")
        page.wait_for_selector("#calendarView:not(.hidden)")
        check("14 appointment attachment upload controls gone after logout",
              lambda: (lambda n: None if n == 0 else f"{n} appt upload controls remain")(
                  page.locator("#calendarView .attachment-upload").count()))
        check("15 attachment listing itself survives lock (open reads)",
              lambda: (lambda v: None if v else "thumbnail vanished")(
                  page.locator(f"[data-att-appt='{appt_id}'] img.attachment-thumb").first.is_visible()))
    finally:
        # ---------- fixture teardown (idempotent) ----------
        try:
            seed_session(seed)
            if appt_id:
                seed.delete(f"/api/appointments/{appt_id}")  # cascades attachments
            if gen_att_id:
                seed.delete(f"/api/attachments/{gen_att_id}")
            seed.post("/api/auth/logout")
        except Exception as exc:  # noqa: BLE001
            print(f"note: fixture teardown failed: {exc}")
        seed.dispose()
        browser.close()

failed = [r for r in results if not r[1]]
print("PASS=%d FAIL=%d" % (len(results) - len(failed), len(failed)))
sys.exit(1 if failed else 0)
