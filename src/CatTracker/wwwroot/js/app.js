(() => {
  'use strict';

  // ---- State ----
  const state = {
    authenticated: false,
    catProfile: null,
    needsOnboarding: false,
    themes: [],
    appointments: [],
    details: [],
    calendarYear: null,
    calendarMonth: null, // 0-11
    selectedDate: null, // 'YYYY-MM-DD' or null
  };

  const DEFAULT_THEME = 'tuxedo';

  // Mirrors Data/ThemeCatalog.cs -- only used to build the dynamic favicon,
  // which (being a standalone file the browser loads on its own) can't read
  // the page's CSS variables. If you add a theme, update both places.
  const THEME_FAVICON_COLORS = {
    tuxedo: { mark: '#111111', patch: '#fafafa', outline: false },
    black: { mark: '#0d0d0d', patch: null, outline: false },
    white: { mark: '#dedcd8', patch: null, outline: true, ink: '#262626' },
    gray: { mark: '#6b6e73', patch: null, outline: false },
    ginger: { mark: '#c1662c', patch: '#ffe7d2', outline: false },
  };

  // ---- API helper ----
  async function api(path, options = {}) {
    const res = await fetch(path, {
      headers: { 'Content-Type': 'application/json', ...(options.headers || {}) },
      credentials: 'same-origin',
      ...options,
    });

    if (res.status === 401) {
      setAuthenticated(false);
    }

    if (!res.ok) {
      let message = `Request failed (${res.status})`;
      try {
        const body = await res.json();
        if (body && body.error) message = body.error;
      } catch {
        /* response had no JSON body */
      }
      throw new Error(message);
    }

    if (res.status === 204) return null;
    const text = await res.text();
    return text ? JSON.parse(text) : null;
  }

  // ---- Toast ----
  let toastTimer = null;
  function showToast(message) {
    const el = document.getElementById('toast');
    el.textContent = message;
    el.classList.remove('hidden');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => el.classList.add('hidden'), 2600);
  }

  // ---- Theme ----
  function applyTheme(themeKey) {
    const key = THEME_FAVICON_COLORS[themeKey] ? themeKey : DEFAULT_THEME;
    document.documentElement.dataset.theme = key;
    updateFavicon(key);
  }

  function updateFavicon(themeKey) {
    const colors = THEME_FAVICON_COLORS[themeKey] || THEME_FAVICON_COLORS[DEFAULT_THEME];
    const strokeAttrs = colors.outline
      ? `stroke="${colors.ink}" stroke-width="2.5" stroke-linejoin="round"`
      : '';
    const eyeFill = colors.outline ? colors.ink : '#fafafa';
    const patch = colors.patch
      ? `<path d="M 60 52 C 69 52, 74 64, 69 82 C 66 94, 54 94, 51 82 C 46 64, 51 52, 60 52 Z" fill="${colors.patch}"/>
         <ellipse cx="40" cy="96" rx="8" ry="6" fill="${colors.patch}"/>
         <ellipse cx="80" cy="96" rx="8" ry="6" fill="${colors.patch}"/>`
      : '';
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120">
      <path d="M 86 90 C 106 86, 114 68, 104 50" fill="none" stroke="${colors.outline ? colors.ink : colors.mark}" stroke-width="9" stroke-linecap="round"/>
      <path d="M 28 98 C 28 66, 42 56, 60 56 C 78 56, 92 66, 92 98 Z" fill="${colors.mark}" ${strokeAttrs}/>
      <polygon points="33,40 42,16 51,42" fill="${colors.mark}" ${strokeAttrs}/>
      <polygon points="69,42 78,16 87,40" fill="${colors.mark}" ${strokeAttrs}/>
      <circle cx="60" cy="46" r="27" fill="${colors.mark}" ${strokeAttrs}/>
      <ellipse cx="50" cy="45" rx="3.6" ry="5" fill="${eyeFill}"/>
      <ellipse cx="70" cy="45" rx="3.6" ry="5" fill="${eyeFill}"/>
      ${patch}
    </svg>`;
    const link = document.getElementById('faviconLink');
    if (link) link.href = `data:image/svg+xml,${encodeURIComponent(svg)}`;
  }

  async function loadThemes() {
    try {
      state.themes = await api('/api/cat/themes');
    } catch {
      state.themes = [];
    }
  }

  // ---- Modal ----
  const modalOverlay = document.getElementById('modalOverlay');
  const modalContent = document.getElementById('modalContent');

  function openModal(html) {
    modalContent.innerHTML = html;
    modalOverlay.classList.remove('hidden');
    document.body.style.overflow = 'hidden';
    const firstField = modalContent.querySelector('input, textarea, select');
    if (firstField) firstField.focus();
  }

  function closeModal() {
    modalOverlay.classList.add('hidden');
    modalContent.innerHTML = '';
    document.body.style.overflow = '';
  }

  function wireModalClose() {
    modalContent.querySelectorAll('[data-close]').forEach((el) => {
      el.addEventListener('click', closeModal);
    });
  }

  modalOverlay.addEventListener('click', (e) => {
    if (e.target === modalOverlay) closeModal();
  });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && !modalOverlay.classList.contains('hidden')) closeModal();
  });

  // ---- Auth ----
  function setAuthenticated(value) {
    state.authenticated = value;
    const btn = document.getElementById('authToggle');
    const label = document.getElementById('authToggleLabel');
    btn.dataset.authed = value ? 'true' : 'false';
    label.textContent = value ? 'Editing unlocked' : 'Unlock';
    document.getElementById('addAppointmentBtn').classList.toggle('hidden', !value);
    document.getElementById('addDetailBtn').classList.toggle('hidden', !value);
    renderAppointmentList();
    renderDetailsList();
  }

  async function refreshAuthStatus() {
    try {
      const status = await api('/api/auth/status');
      setAuthenticated(!!status.authenticated);
    } catch {
      setAuthenticated(false);
    }
  }

  function openLoginModal() {
    openModal(`
      <button class="modal-close" type="button" data-close>&times;</button>
      <h2>Unlock editing</h2>
      <form id="loginForm">
        <div class="field">
          <label for="loginPassword">Password</label>
          <input type="password" id="loginPassword" autocomplete="current-password" />
        </div>
        <div class="field-error hidden" id="loginError"></div>
        <div class="modal-actions">
          <button type="button" class="secondary-btn" data-close>Cancel</button>
          <button type="submit" class="primary-btn">Unlock</button>
        </div>
      </form>
    `);
    wireModalClose();

    document.getElementById('loginForm').addEventListener('submit', async (e) => {
      e.preventDefault();
      const password = document.getElementById('loginPassword').value;
      const errorEl = document.getElementById('loginError');
      errorEl.classList.add('hidden');
      try {
        await api('/api/auth/login', { method: 'POST', body: JSON.stringify({ password }) });
        setAuthenticated(true);
        closeModal();
        showToast('Editing unlocked');
      } catch (err) {
        errorEl.textContent = err.message.includes('401') ? 'Wrong password.' : err.message;
        errorEl.classList.remove('hidden');
      }
    });
  }

  document.getElementById('authToggle').addEventListener('click', async () => {
    if (state.authenticated) {
      await api('/api/auth/logout', { method: 'POST' }).catch(() => {});
      setAuthenticated(false);
      showToast('Locked');
    } else {
      openLoginModal();
    }
  });

  // ---- Cat status / hero ----
  async function loadCatStatus() {
    const status = await api('/api/cat');
    state.catProfile = status;
    state.needsOnboarding = status.needsOnboarding;

    applyTheme(status.theme);

    const onboardingCard = document.getElementById('onboardingCard');
    const appContent = document.getElementById('appContent');
    const settingsBtn = document.getElementById('settingsBtn');

    if (status.needsOnboarding) {
      onboardingCard.classList.remove('hidden');
      appContent.classList.add('hidden');
      settingsBtn.classList.add('hidden');
      document.title = 'Cat Age Tracker';
      document.getElementById('catName').textContent = 'Cat Age Tracker';
      document.getElementById('catMeta').textContent = "Let's get your cat set up";
      document.getElementById('lifeStageEyebrow').textContent = '\u00A0';
      return;
    }

    onboardingCard.classList.add('hidden');
    appContent.classList.remove('hidden');
    settingsBtn.classList.remove('hidden');

    document.getElementById('catName').textContent = status.name;
    document.getElementById('catMeta').textContent = [status.colorPattern, status.breed].filter(Boolean).join(' · ');
    document.getElementById('lifeStageEyebrow').textContent = status.lifeStageName;
    document.getElementById('ageFormatted').textContent = status.formattedAge;
    document.getElementById('humanYears').textContent = `~${status.humanEquivalentYears} yrs`;
    document.getElementById('stageName').textContent = status.lifeStageName;
    document.getElementById('stageRange').textContent = status.lifeStageRangeLabel;
    document.getElementById('stageSummary').textContent = status.lifeStageSummary;
    document.getElementById('stageDescription').textContent = status.lifeStageDescription;
    document.title = `${status.name} · ${status.formattedAge} old`;

    const birthDate = new Date(`${status.birthDate}T00:00:00`);
    document.getElementById('birthdateNote').textContent = `Born ${birthDate.toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    })}`;

    const slider = document.getElementById('timelineSlider');
    slider.value = Math.min(25, status.catYears).toFixed(2);

    // Render "today" directly from the status payload rather than round-tripping
    // through /age-at, so the initial view lands exactly on "Today" instead of
    // drifting a week or two due to slider step rounding.
    showTodayOnTimeline(status);
  }

  // ---- Profile (onboarding + later editing share this form) ----
  function themeSwatchesHtml(selectedKey) {
    return state.themes
      .map(
        (t) => `
      <button type="button" class="theme-swatch" data-theme-key="${t.key}"
              aria-pressed="${t.key === selectedKey ? 'true' : 'false'}">
        <span class="swatch-dot" style="background:${t.swatchColor}"></span>
        <span>
          <span class="swatch-name">${escapeHtml(t.name)}</span>
          <span class="swatch-desc">${escapeHtml(t.description)}</span>
        </span>
      </button>`
      )
      .join('');
  }

  function openProfileModal() {
    const isOnboarding = state.needsOnboarding;
    const profile = state.catProfile || {};
    const needsPassword = !state.authenticated;
    let selectedTheme = (!isOnboarding && profile.theme) || DEFAULT_THEME;

    const nameVal = isOnboarding ? '' : profile.name || '';
    const breedVal = isOnboarding ? '' : profile.breed || '';
    const colorVal = isOnboarding ? '' : profile.colorPattern || '';
    const birthVal = isOnboarding || !profile.birthDate || profile.birthDate === '0001-01-01' ? '' : profile.birthDate;

    const now = new Date();
    const todayStr = toDateKey(now.getFullYear(), now.getMonth(), now.getDate());

    openModal(`
      <button class="modal-close" type="button" data-close>&times;</button>
      <h2>${isOnboarding ? "Let's set up your cat" : 'Edit cat profile'}</h2>
      <form id="profileForm">
        <div class="field">
          <label for="profileName">Name</label>
          <input type="text" id="profileName" value="${escapeHtml(nameVal)}" required />
        </div>
        <div class="field">
          <label for="profileBirthDate">Birthdate</label>
          <input type="date" id="profileBirthDate" value="${birthVal}" max="${todayStr}" required />
        </div>
        <div class="field">
          <label for="profileBreed">Breed (optional)</label>
          <input type="text" id="profileBreed" value="${escapeHtml(breedVal)}" />
        </div>
        <div class="field">
          <label for="profileColor">Color / pattern (optional)</label>
          <input type="text" id="profileColor" value="${escapeHtml(colorVal)}" placeholder="e.g. Orange tabby" />
        </div>
        <div class="field">
          <label>Theme</label>
          <div class="theme-picker" id="profileThemePicker">
            ${themeSwatchesHtml(selectedTheme)}
          </div>
        </div>
        ${
          needsPassword
            ? `<div class="field">
                 <label for="profilePassword">Password</label>
                 <input type="password" id="profilePassword" autocomplete="current-password" />
               </div>`
            : ''
        }
        <div class="field-error hidden" id="profileError"></div>
        <div class="modal-actions">
          <button type="button" class="secondary-btn" data-close>Cancel</button>
          <button type="submit" class="primary-btn">${isOnboarding ? 'Save & start' : 'Save'}</button>
        </div>
      </form>
    `);
    wireModalClose();

    const picker = document.getElementById('profileThemePicker');
    picker.querySelectorAll('.theme-swatch').forEach((btn) => {
      btn.addEventListener('click', () => {
        selectedTheme = btn.dataset.themeKey;
        picker.querySelectorAll('.theme-swatch').forEach((b) => {
          b.setAttribute('aria-pressed', b === btn ? 'true' : 'false');
        });
      });
    });

    document.getElementById('profileForm').addEventListener('submit', async (e) => {
      e.preventDefault();
      const name = document.getElementById('profileName').value.trim();
      const birthDate = document.getElementById('profileBirthDate').value;
      const breed = document.getElementById('profileBreed').value.trim();
      const colorPattern = document.getElementById('profileColor').value.trim();
      const errorEl = document.getElementById('profileError');
      errorEl.classList.add('hidden');

      if (!name || !birthDate) {
        errorEl.textContent = 'Name and birthdate are required.';
        errorEl.classList.remove('hidden');
        return;
      }

      try {
        if (needsPassword) {
          const password = document.getElementById('profilePassword').value;
          try {
            await api('/api/auth/login', { method: 'POST', body: JSON.stringify({ password }) });
          } catch {
            errorEl.textContent = 'Wrong password.';
            errorEl.classList.remove('hidden');
            return;
          }
          setAuthenticated(true);
        }

        await api('/api/cat', {
          method: 'PUT',
          body: JSON.stringify({ name, breed, colorPattern, birthDate, theme: selectedTheme }),
        });

        closeModal();
        showToast(isOnboarding ? 'All set!' : 'Profile updated');
        await loadCatStatus();
      } catch (err) {
        // Only the save call (not login) can land here now.
        if (err.message.includes('401')) {
          setAuthenticated(false);
          errorEl.textContent = 'Your session expired. Close this, click Unlock, and try again.';
        } else {
          errorEl.textContent = err.message;
        }
        errorEl.classList.remove('hidden');
      }
    });
  }

  document.getElementById('startOnboardingBtn').addEventListener('click', () => openProfileModal());
  document.getElementById('settingsBtn').addEventListener('click', () => openProfileModal());

  // ---- Timeline ----
  function buildTapeLabels() {
    const container = document.getElementById('tapeLabels');
    const marks = [0, 5, 10, 15, 20, 25];
    container.innerHTML = marks.map((m) => `<span>${m}</span>`).join('');
  }

  function renderTimelineData(data) {
    const date = new Date(`${data.date}T00:00:00`);
    document.getElementById('timelineDate').textContent = date.toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
    document.getElementById('timelineRelative').textContent = data.isToday ? 'Today' : data.relativeLabel;
    document.getElementById('timelineAge').textContent = data.formattedAge;
    document.getElementById('timelineHuman').textContent = `~${data.humanEquivalentYears} yrs`;
    document.getElementById('timelineStage').textContent = data.lifeStageName;
    document.getElementById('timelineDesc').textContent = data.lifeStageDescription;
  }

  function showTodayOnTimeline(status) {
    const now = new Date();
    renderTimelineData({
      date: toDateKey(now.getFullYear(), now.getMonth(), now.getDate()),
      formattedAge: status.formattedAge,
      humanEquivalentYears: status.humanEquivalentYears,
      lifeStageName: status.lifeStageName,
      lifeStageDescription: status.lifeStageDescription,
      isToday: true,
      relativeLabel: 'Today',
    });
  }

  async function refreshTimeline() {
    const slider = document.getElementById('timelineSlider');
    const years = parseFloat(slider.value);
    const data = await api(`/api/cat/age-at?years=${years}`);
    renderTimelineData(data);
  }

  let timelineDebounce = null;
  document.getElementById('timelineSlider').addEventListener('input', () => {
    clearTimeout(timelineDebounce);
    timelineDebounce = setTimeout(refreshTimeline, 60);
  });

  document.getElementById('resetTimeline').addEventListener('click', () => {
    if (!state.catProfile) return;
    const slider = document.getElementById('timelineSlider');
    slider.value = Math.min(25, state.catProfile.catYears).toFixed(2);
    showTodayOnTimeline(state.catProfile);
  });

  // ---- Calendar ----
  function pad2(n) {
    return String(n).padStart(2, '0');
  }
  function toDateKey(y, m, d) {
    return `${y}-${pad2(m + 1)}-${pad2(d)}`;
  }

  function renderCalendar() {
    const grid = document.getElementById('calendarGrid');
    const label = document.getElementById('calendarMonthLabel');
    const { calendarYear: year, calendarMonth: month } = state;

    label.textContent = new Date(year, month, 1).toLocaleDateString(undefined, {
      month: 'long',
      year: 'numeric',
    });

    const weekdayNames = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];
    let html = weekdayNames.map((w) => `<div class="calendar-weekday">${w}</div>`).join('');

    const firstOfMonth = new Date(year, month, 1);
    const startOffset = firstOfMonth.getDay();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const daysInPrevMonth = new Date(year, month, 0).getDate();

    const now = new Date();
    const todayKey = toDateKey(now.getFullYear(), now.getMonth(), now.getDate());

    const apptDaysThisMonth = new Set(
      state.appointments
        .filter((a) => {
          const d = new Date(a.when);
          return d.getFullYear() === year && d.getMonth() === month;
        })
        .map((a) => new Date(a.when).getDate())
    );

    const cells = [];
    for (let i = 0; i < startOffset; i++) {
      cells.push({ dayNum: daysInPrevMonth - startOffset + 1 + i, outside: true, key: null });
    }
    for (let d = 1; d <= daysInMonth; d++) {
      cells.push({ dayNum: d, outside: false, key: toDateKey(year, month, d) });
    }
    let nextMonthDay = 1;
    while (cells.length % 7 !== 0) {
      cells.push({ dayNum: nextMonthDay, outside: true, key: null });
      nextMonthDay++;
    }

    html += cells
      .map((c) => {
        const classes = ['calendar-day'];
        if (c.outside) classes.push('is-outside');
        if (c.key === todayKey) classes.push('is-today');
        if (c.key && c.key === state.selectedDate) classes.push('is-selected');
        const dot = !c.outside && apptDaysThisMonth.has(c.dayNum) ? '<span class="dot"></span>' : '';
        const tabindex = c.outside ? 'tabindex="-1"' : '';
        return `<button type="button" class="${classes.join(' ')}" data-key="${c.key || ''}" ${tabindex}>${c.dayNum}${dot}</button>`;
      })
      .join('');

    grid.innerHTML = html;

    grid.querySelectorAll('.calendar-day').forEach((btn) => {
      const key = btn.dataset.key;
      if (!key) return;
      btn.addEventListener('click', () => {
        state.selectedDate = state.selectedDate === key ? null : key;
        renderCalendar();
        renderAppointmentList();
      });
    });
  }

  document.getElementById('prevMonth').addEventListener('click', () => {
    state.calendarMonth -= 1;
    if (state.calendarMonth < 0) {
      state.calendarMonth = 11;
      state.calendarYear -= 1;
    }
    renderCalendar();
  });

  document.getElementById('nextMonth').addEventListener('click', () => {
    state.calendarMonth += 1;
    if (state.calendarMonth > 11) {
      state.calendarMonth = 0;
      state.calendarYear += 1;
    }
    renderCalendar();
  });

  const categoryLabels = { Vet: 'Vet', Grooming: 'Grooming', Medication: 'Medication', Other: 'Other' };

  function renderAppointmentList() {
    const container = document.getElementById('appointmentList');
    let list = state.appointments.slice().sort((a, b) => new Date(a.when) - new Date(b.when));

    if (state.selectedDate) {
      list = list.filter((a) => {
        const d = new Date(a.when);
        return toDateKey(d.getFullYear(), d.getMonth(), d.getDate()) === state.selectedDate;
      });
    } else {
      const now = new Date();
      const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
      list = list.filter((a) => new Date(a.when) >= startOfToday).slice(0, 8);
    }

    if (list.length === 0) {
      const message = state.selectedDate ? 'No appointments on this day.' : 'No upcoming appointments.';
      container.innerHTML = `<p class="empty-note">${message}</p>`;
      return;
    }

    container.innerHTML = list
      .map((a) => {
        const d = new Date(a.when);
        const whenStr = d.toLocaleString(undefined, {
          month: 'short',
          day: 'numeric',
          year: 'numeric',
          hour: 'numeric',
          minute: '2-digit',
        });
        const actions = state.authenticated
          ? `<div class="row-actions">
               <button type="button" class="icon-btn" data-edit-appt="${a.id}" aria-label="Edit appointment">✎</button>
               <button type="button" class="icon-btn" data-delete-appt="${a.id}" aria-label="Delete appointment">🗑</button>
             </div>`
          : '';
        return `
          <div class="appointment-row">
            <div>
              <div class="when">${whenStr}</div>
              <div class="title">${escapeHtml(a.title)}</div>
              ${a.notes ? `<div class="notes">${escapeHtml(a.notes)}</div>` : ''}
              <span class="category-tag">${categoryLabels[a.category] || a.category}</span>
            </div>
            ${actions}
          </div>`;
      })
      .join('');

    container.querySelectorAll('[data-edit-appt]').forEach((btn) => {
      btn.addEventListener('click', () => openAppointmentModal(btn.dataset.editAppt));
    });
    container.querySelectorAll('[data-delete-appt]').forEach((btn) => {
      btn.addEventListener('click', () => confirmDeleteAppointment(btn.dataset.deleteAppt));
    });
  }

  async function loadAppointments() {
    state.appointments = await api('/api/appointments');
    renderCalendar();
    renderAppointmentList();
  }

  function openAppointmentModal(id) {
    const existing = id ? state.appointments.find((a) => a.id === id) : null;
    const now = new Date();
    const defaultDate = state.selectedDate || toDateKey(now.getFullYear(), now.getMonth(), now.getDate());

    let dateVal = defaultDate;
    let timeVal = '10:00';
    if (existing) {
      const d = new Date(existing.when);
      dateVal = toDateKey(d.getFullYear(), d.getMonth(), d.getDate());
      timeVal = `${pad2(d.getHours())}:${pad2(d.getMinutes())}`;
    }

    const categoryOption = (value, text) =>
      `<option value="${value}" ${existing ? (existing.category === value ? 'selected' : '') : value === 'Other' ? 'selected' : ''}>${text}</option>`;

    openModal(`
      <button class="modal-close" type="button" data-close>&times;</button>
      <h2>${existing ? 'Edit appointment' : 'New appointment'}</h2>
      <form id="apptForm">
        <div class="field">
          <label for="apptTitle">Title</label>
          <input type="text" id="apptTitle" value="${existing ? escapeHtml(existing.title) : ''}" required />
        </div>
        <div class="field">
          <label for="apptDate">Date</label>
          <input type="date" id="apptDate" value="${dateVal}" required />
        </div>
        <div class="field">
          <label for="apptTime">Time</label>
          <input type="time" id="apptTime" value="${timeVal}" required />
        </div>
        <div class="field">
          <label for="apptCategory">Category</label>
          <select id="apptCategory">
            ${categoryOption('Vet', 'Vet')}
            ${categoryOption('Grooming', 'Grooming')}
            ${categoryOption('Medication', 'Medication')}
            ${categoryOption('Other', 'Other')}
          </select>
        </div>
        <div class="field">
          <label for="apptNotes">Notes</label>
          <textarea id="apptNotes">${existing ? escapeHtml(existing.notes || '') : ''}</textarea>
        </div>
        <div class="field-error hidden" id="apptError"></div>
        <div class="modal-actions">
          <button type="button" class="secondary-btn" data-close>Cancel</button>
          <button type="submit" class="primary-btn">Save</button>
        </div>
      </form>
    `);
    wireModalClose();

    document.getElementById('apptForm').addEventListener('submit', async (e) => {
      e.preventDefault();
      const title = document.getElementById('apptTitle').value.trim();
      const date = document.getElementById('apptDate').value;
      const time = document.getElementById('apptTime').value;
      const category = document.getElementById('apptCategory').value;
      const notes = document.getElementById('apptNotes').value.trim();
      const errorEl = document.getElementById('apptError');
      errorEl.classList.add('hidden');

      if (!title || !date || !time) {
        errorEl.textContent = 'Title, date, and time are required.';
        errorEl.classList.remove('hidden');
        return;
      }

      const payload = { title, notes: notes || null, when: `${date}T${time}:00`, category };

      try {
        if (existing) {
          await api(`/api/appointments/${existing.id}`, { method: 'PUT', body: JSON.stringify(payload) });
        } else {
          await api('/api/appointments', { method: 'POST', body: JSON.stringify(payload) });
        }
        closeModal();
        showToast(existing ? 'Appointment updated' : 'Appointment added');
        await loadAppointments();
      } catch (err) {
        errorEl.textContent = err.message;
        errorEl.classList.remove('hidden');
      }
    });
  }

  function confirmDeleteAppointment(id) {
    const appt = state.appointments.find((a) => a.id === id);
    openModal(`
      <button class="modal-close" type="button" data-close>&times;</button>
      <h2>Delete appointment?</h2>
      <p>"${escapeHtml(appt ? appt.title : 'This appointment')}" will be permanently removed.</p>
      <div class="modal-actions">
        <button type="button" class="secondary-btn" data-close>Cancel</button>
        <button type="button" class="danger-btn" id="confirmDeleteApptBtn">Delete</button>
      </div>
    `);
    wireModalClose();

    document.getElementById('confirmDeleteApptBtn').addEventListener('click', async () => {
      try {
        await api(`/api/appointments/${id}`, { method: 'DELETE' });
        closeModal();
        showToast('Appointment deleted');
        await loadAppointments();
      } catch (err) {
        showToast(err.message);
      }
    });
  }

  document.getElementById('addAppointmentBtn').addEventListener('click', () => openAppointmentModal(null));

  // ---- Details ----
  async function loadDetails() {
    state.details = await api('/api/details');
    renderDetailsList();
  }

  function renderDetailsList() {
    const container = document.getElementById('detailsList');
    if (state.details.length === 0) {
      container.innerHTML = `<p class="empty-note">Nothing saved yet.</p>`;
      return;
    }

    const groups = new Map();
    state.details.forEach((d) => {
      if (!groups.has(d.category)) groups.set(d.category, []);
      groups.get(d.category).push(d);
    });

    let html = '';
    for (const [category, items] of groups) {
      html += `<div class="details-group"><h3>${escapeHtml(category)}</h3>`;
      html += items
        .map((d) => {
          const dateStr = d.date ? new Date(`${d.date}T00:00:00`).toLocaleDateString() : '';
          const actions = state.authenticated
            ? `<div class="row-actions">
                 <button type="button" class="icon-btn" data-edit-detail="${d.id}" aria-label="Edit detail">✎</button>
                 <button type="button" class="icon-btn" data-delete-detail="${d.id}" aria-label="Delete detail">🗑</button>
               </div>`
            : '';
          return `
            <div class="detail-row">
              <div>
                <div class="title">${escapeHtml(d.title)}</div>
                ${dateStr ? `<div class="date">${dateStr}</div>` : ''}
                ${d.content ? `<div class="content">${escapeHtml(d.content)}</div>` : ''}
              </div>
              ${actions}
            </div>`;
        })
        .join('');
      html += `</div>`;
    }
    container.innerHTML = html;

    container.querySelectorAll('[data-edit-detail]').forEach((btn) => {
      btn.addEventListener('click', () => openDetailModal(btn.dataset.editDetail));
    });
    container.querySelectorAll('[data-delete-detail]').forEach((btn) => {
      btn.addEventListener('click', () => confirmDeleteDetail(btn.dataset.deleteDetail));
    });
  }

  function openDetailModal(id) {
    const existing = id ? state.details.find((d) => d.id === id) : null;

    openModal(`
      <button class="modal-close" type="button" data-close>&times;</button>
      <h2>${existing ? 'Edit detail' : 'New detail'}</h2>
      <form id="detailForm">
        <div class="field">
          <label for="detailCategory">Category</label>
          <input type="text" id="detailCategory" placeholder="e.g. Medical, Preferences, Contacts"
                 value="${existing ? escapeHtml(existing.category) : ''}" />
        </div>
        <div class="field">
          <label for="detailTitle">Title</label>
          <input type="text" id="detailTitle" value="${existing ? escapeHtml(existing.title) : ''}" required />
        </div>
        <div class="field">
          <label for="detailDate">Date (optional)</label>
          <input type="date" id="detailDate" value="${existing && existing.date ? existing.date : ''}" />
        </div>
        <div class="field">
          <label for="detailContent">Details</label>
          <textarea id="detailContent">${existing ? escapeHtml(existing.content || '') : ''}</textarea>
        </div>
        <div class="field-error hidden" id="detailError"></div>
        <div class="modal-actions">
          <button type="button" class="secondary-btn" data-close>Cancel</button>
          <button type="submit" class="primary-btn">Save</button>
        </div>
      </form>
    `);
    wireModalClose();

    document.getElementById('detailForm').addEventListener('submit', async (e) => {
      e.preventDefault();
      const category = document.getElementById('detailCategory').value.trim() || 'General';
      const title = document.getElementById('detailTitle').value.trim();
      const date = document.getElementById('detailDate').value || null;
      const content = document.getElementById('detailContent').value.trim();
      const errorEl = document.getElementById('detailError');
      errorEl.classList.add('hidden');

      if (!title) {
        errorEl.textContent = 'Title is required.';
        errorEl.classList.remove('hidden');
        return;
      }

      const payload = { category, title, content, date };

      try {
        if (existing) {
          await api(`/api/details/${existing.id}`, { method: 'PUT', body: JSON.stringify(payload) });
        } else {
          await api('/api/details', { method: 'POST', body: JSON.stringify(payload) });
        }
        closeModal();
        showToast(existing ? 'Detail updated' : 'Detail added');
        await loadDetails();
      } catch (err) {
        errorEl.textContent = err.message;
        errorEl.classList.remove('hidden');
      }
    });
  }

  function confirmDeleteDetail(id) {
    const detail = state.details.find((d) => d.id === id);
    openModal(`
      <button class="modal-close" type="button" data-close>&times;</button>
      <h2>Delete this detail?</h2>
      <p>"${escapeHtml(detail ? detail.title : 'This entry')}" will be permanently removed.</p>
      <div class="modal-actions">
        <button type="button" class="secondary-btn" data-close>Cancel</button>
        <button type="button" class="danger-btn" id="confirmDeleteDetailBtn">Delete</button>
      </div>
    `);
    wireModalClose();

    document.getElementById('confirmDeleteDetailBtn').addEventListener('click', async () => {
      try {
        await api(`/api/details/${id}`, { method: 'DELETE' });
        closeModal();
        showToast('Detail deleted');
        await loadDetails();
      } catch (err) {
        showToast(err.message);
      }
    });
  }

  document.getElementById('addDetailBtn').addEventListener('click', () => openDetailModal(null));

  // ---- Helpers ----
  function escapeHtml(str) {
    return String(str ?? '').replace(/[&<>"']/g, (c) => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#39;',
    }[c]));
  }

  // ---- Init ----
  async function init() {
    const now = new Date();
    state.calendarYear = now.getFullYear();
    state.calendarMonth = now.getMonth();

    buildTapeLabels();

    await refreshAuthStatus();
    await loadThemes();
    await loadCatStatus();
    await loadAppointments();
    await loadDetails();
  }

  init().catch((err) => {
    console.error(err);
    showToast('Something went wrong loading your cat data.');
  });
})();
