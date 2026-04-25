// Shell.jsx — reusable application chrome (sidebar + main pane).
// Icons are rendered as <img> with a CSS filter matrix, matching how
// the real NinetyNine app tints stroke-only Phosphor SVGs.

const ICONS = {
  house:      'assets/nn-icons/house.svg',
  users:      'assets/nn-icons/users-three.svg',
  plus:       'assets/nn-icons/plus.svg',
  list:       'assets/nn-icons/list.svg',
  chart:      'assets/nn-icons/chart-bar.svg',
  mapPin:     'assets/nn-icons/map-pin.svg',
  globe:      'assets/nn-icons/globe.svg',
  bell:       'assets/nn-icons/bell.svg',
  user:       'assets/nn-icons/user.svg',
  info:       'assets/nn-icons/info.svg',
  trophy:     'assets/nn-icons/trophy.svg',
  trophyFill: 'assets/nn-icons/trophy-fill.svg',
  target:     'assets/nn-icons/target.svg',
  targetFill: 'assets/nn-icons/target-fill.svg',
  check:      'assets/nn-icons/check.svg',
  checkFill:  'assets/nn-icons/check-fill.svg',
  chartLine:  'assets/nn-icons/chart-line-up.svg',
  controller: 'assets/nn-icons/game-controller.svg',
  starFill:   'assets/nn-icons/star-fill.svg',
  caretDown:  'assets/nn-icons/caret-down.svg',
  caretRight: 'assets/nn-icons/caret-right.svg',
  search:     'assets/nn-icons/search.svg',
  crosshair:  'assets/nn-icons/crosshair.svg',
  signIn:     'assets/nn-icons/sign-in.svg',
};

// Icon: <img> tinted via CSS filter. `tone` maps to .nn-icon--<tone>
// classes in theme.css: primary | secondary | tertiary | teal | gold | on-accent.
// Omit `tone` to get the default (--nn-text-secondary).
function Icon({ name, size = 18, tone, className = '', style }) {
  const src = ICONS[name];
  if (!src) return null;
  const toneClass = tone ? ` nn-icon--${tone}` : '';
  return (
    <img
      src={src}
      alt=""
      aria-hidden="true"
      className={`nn-icon${toneClass} ${className}`.trim()}
      style={{ width: size, height: size, ...style }}
    />
  );
}

const NAV_ITEMS = [
  { key: 'home',          label: 'Home',          icon: 'house',      href: '/' },
  { key: 'friends',       label: 'Friends',       icon: 'users',      href: '/friends', badge: 3 },
  { key: 'new-game',      label: 'New Game',      icon: 'plus',       href: '/games/new' },
  { key: 'history',       label: 'History',       icon: 'list',       href: '/games' },
  { key: 'stats',         label: 'Stats',         icon: 'chart',      href: '/stats' },
  { key: 'venues',        label: 'Venues',        icon: 'mapPin',     href: '/venues' },
  { key: 'communities',   label: 'Communities',   icon: 'globe',      href: '/communities' },
  { key: 'notifications', label: 'Notifications', icon: 'bell',       href: '/notifications', badge: 2 },
  { key: 'profile',       label: 'Profile',       icon: 'user',       href: '/players/me' },
  { key: 'about',         label: 'About',         icon: 'info',       href: '/about' },
];

function Sidebar({ active = 'home', user = { initials: 'SP', name: 'Sam Pocket', handle: '@sam' } }) {
  return (
    <aside className="nn-sidebar">
      <div className="nn-sidebar__brand">
        <img className="nn-brand-glyph" src="assets/nn-icons/cue-sports.svg" alt="" aria-hidden="true" />
        <span>NinetyNine</span>
      </div>
      <nav className="nn-sidebar__nav">
        {NAV_ITEMS.map((item) => {
          const isActive = item.key === active;
          return (
            <a key={item.key} href="#" className={`nn-nav__link ${isActive ? 'active' : ''}`}>
              <Icon name={item.icon} size={20} tone={isActive ? 'teal' : 'secondary'} />
              <span className="nn-nav__label">{item.label}</span>
              {item.badge ? <span className="nn-nav__badge">{item.badge}</span> : null}
            </a>
          );
        })}
      </nav>
      <div className="nn-sidebar__footer">
        <div className="nn-user-menu">
          <div className="nn-user-menu__avatar">{user.initials}</div>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div className="nn-user-menu__name">{user.name}</div>
            <div className="nn-user-menu__meta">{user.handle}</div>
          </div>
          <Icon name="caretDown" size={14} tone="tertiary" />
        </div>
      </div>
    </aside>
  );
}

function Shell({ active, user, children, noPadding }) {
  return (
    <div className="nn-artboard">
      <Sidebar active={active} user={user} />
      <main className="nn-main">
        <div className="nn-content" style={noPadding ? { padding: 0 } : null}>
          {children}
        </div>
      </main>
    </div>
  );
}

Object.assign(window, { Icon, Shell, Sidebar, NAV_ITEMS });
