/* ============================================================
   BigBirdBot Documentation
   main.js
   ============================================================ */


// ── Command search ───────────────────────────────────────────

function filterCmds(q) {
  q = q.toLowerCase().trim();
  // Sync both search inputs
  const desktop = document.getElementById('searchInput');
  const mobile  = document.getElementById('mobileSearch');
  if (desktop && document.activeElement !== desktop) desktop.value = q;
  if (mobile  && document.activeElement !== mobile)  mobile.value  = q;

  document.querySelectorAll('.cmd-card').forEach(card => {
    const text = card.textContent.toLowerCase();
    const name = (card.dataset.cmd || '').toLowerCase();
    card.classList.toggle('hidden', !!q && !text.includes(q) && !name.includes(q));
  });
}


// ── Mobile drawer ────────────────────────────────────────────

const hamburger = document.getElementById('navHamburger');
const drawer    = document.getElementById('mobileDrawer');
const overlay   = document.getElementById('drawerOverlay');
const closeBtn  = document.getElementById('drawerClose');

function openDrawer() {
  drawer.classList.add('open');
  overlay.classList.add('open');
  hamburger.classList.add('open');
  hamburger.setAttribute('aria-expanded', 'true');
  drawer.setAttribute('aria-hidden', 'false');
  document.body.style.overflow = 'hidden';
}

function closeDrawer() {
  drawer.classList.remove('open');
  overlay.classList.remove('open');
  hamburger.classList.remove('open');
  hamburger.setAttribute('aria-expanded', 'false');
  drawer.setAttribute('aria-hidden', 'true');
  document.body.style.overflow = '';
}

hamburger.addEventListener('click', () => {
  drawer.classList.contains('open') ? closeDrawer() : openDrawer();
});

closeBtn.addEventListener('click', closeDrawer);
overlay.addEventListener('click', closeDrawer);

// Close drawer and jump to section when a link is tapped
document.querySelectorAll('[data-drawer-link]').forEach(link => {
  link.addEventListener('click', () => {
    closeDrawer();
  });
});

// Close on Escape
document.addEventListener('keydown', e => {
  if (e.key === 'Escape' && drawer.classList.contains('open')) closeDrawer();
});


// ── Active nav highlight on scroll ──────────────────────────

const sections  = document.querySelectorAll('.section');
const navLinks  = document.querySelectorAll('.topnav-link');
const mobileLinks = document.querySelectorAll('.mobile-nav-link');

const activeObserver = new IntersectionObserver(entries => {
  entries.forEach(entry => {
    if (entry.isIntersecting) {
      const id = entry.target.id;
      navLinks.forEach(l => l.classList.remove('active'));
      mobileLinks.forEach(l => l.classList.remove('active'));

      const desktopMatch = document.querySelector(`.topnav-link[href="#${id}"]`);
      const mobileMatch  = document.querySelector(`.mobile-nav-link[href="#${id}"]`);
      if (desktopMatch) desktopMatch.classList.add('active');
      if (mobileMatch)  mobileMatch.classList.add('active');
    }
  });
}, { rootMargin: '-20% 0px -70% 0px' });

sections.forEach(s => activeObserver.observe(s));


// ── Scroll reveal ────────────────────────────────────────────

const revealObserver = new IntersectionObserver(entries => {
  entries.forEach(entry => {
    if (entry.isIntersecting) {
      entry.target.classList.add('visible');
      revealObserver.unobserve(entry.target);
    }
  });
}, { rootMargin: '0px 0px -40px 0px' });

// Stagger children of grids
document.querySelectorAll('.cmd-grid, .feat-grid, .stat-strip').forEach(grid => {
  [...grid.children].forEach((child, i) => {
    child.classList.add('reveal');
    child.style.transitionDelay = `${i * 0.04}s`;
    revealObserver.observe(child);
  });
});

// Remaining standalone reveal elements
document.querySelectorAll('.reveal').forEach(el => revealObserver.observe(el));
