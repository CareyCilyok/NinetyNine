// Screens.jsx — one component per mocked screen.
// Each screen renders inside <Shell> so the sidebar chrome is identical.
//
// Screens included:
//   HomeScreen         — /  (authenticated dashboard)
//   NewGameScreen      — /games/new
//   PlayScreen         — /games/{id}/play  (live scoring, mid-game)
//   CompleteScreen     — /games/{id}/play  (perfect-game celebration)
//   HistoryScreen      — /games
//   MyStatsScreen      — /stats/me
//   LeaderboardScreen  — /stats
//   ProfileScreen      — /players/me
//   FriendsScreen      — /friends
//   NotificationsScreen — /notifications

// --------- Shared fragments ---------
const SectionHeader = ({ title, sub, action }) => (
  <header style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 20, marginBottom: 16 }}>
    <div>
      <h2 style={{ margin: 0, fontSize: 18, fontWeight: 700, color: 'var(--nn-text-primary)', letterSpacing: '-0.01em' }}>{title}</h2>
      {sub ? <p style={{ margin: '4px 0 0', fontSize: 13, color: 'var(--nn-text-secondary)' }}>{sub}</p> : null}
    </div>
    {action}
  </header>
);

const StatPill = ({ label, value, suffix, icon, tone }) => (
  <div className="nn-card" style={{
    padding: '16px 18px', display: 'flex', alignItems: 'center', gap: 14,
    borderColor: tone === 'gold' ? 'rgba(var(--nn-accent-gold-rgb),0.35)' : undefined,
  }}>
    <div style={{
      width: 36, height: 36, borderRadius: 10,
      background: tone === 'gold' ? 'var(--nn-accent-gold-muted)' :
                  tone === 'teal' ? 'rgba(var(--nn-accent-teal-rgb), 0.14)' :
                  'var(--nn-bg-tertiary)',
      color: tone === 'gold' ? 'var(--nn-accent-gold)' :
             tone === 'teal' ? 'var(--nn-accent-teal)' :
             'var(--nn-text-secondary)',
      display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
    }}>
      <Icon name={icon} size={18} />
    </div>
    <div style={{ minWidth: 0 }}>
      <div className="mono" style={{ fontSize: 22, fontWeight: 700, color: 'var(--nn-text-primary)', lineHeight: 1, letterSpacing: '-0.01em' }}>
        {value}{suffix ? <span style={{ fontSize: 13, color: 'var(--nn-text-tertiary)', fontWeight: 500, marginLeft: 4 }}>{suffix}</span> : null}
      </div>
      <div style={{ fontSize: 11, color: 'var(--nn-text-tertiary)', marginTop: 4, textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{label}</div>
    </div>
  </div>
);

// =========================================================
// HOME
// =========================================================
function HomeScreen() {
  return (
    <Shell active="home">
      {/* Hero */}
      <section style={{
        position: 'relative', borderRadius: 'var(--nn-radius-lg)', overflow: 'hidden',
        background: 'radial-gradient(ellipse at 30% 40%, rgba(31,184,146,0.22), transparent 60%), radial-gradient(ellipse at 80% 110%, rgba(224,180,108,0.18), transparent 55%), linear-gradient(155deg, #0b2420 0%, #0e1618 55%, #141719 100%)',
        padding: '38px 38px 34px', marginBottom: 28,
        border: '1px solid var(--nn-border-subtle)',
      }}>
        {/* Billiard felt pattern overlay */}
        <div aria-hidden style={{
          position: 'absolute', inset: 0, opacity: 0.4, pointerEvents: 'none',
          backgroundImage: 'radial-gradient(circle at 85% 20%, rgba(224,180,108,0.12), transparent 35%), repeating-linear-gradient(-15deg, transparent 0 4px, rgba(255,255,255,0.015) 4px 5px)',
        }} />
        <div style={{ position: 'relative', maxWidth: 560 }}>
          <div style={{ fontSize: 11, color: 'var(--nn-accent-gold)', textTransform: 'uppercase', letterSpacing: '0.22em', fontWeight: 700 }}>Pool scorekeeper</div>
          <h1 style={{
            fontSize: 56, fontWeight: 800, letterSpacing: '-0.035em',
            margin: '12px 0 12px', color: 'var(--nn-text-primary)',
            lineHeight: 0.95,
          }}>
            Ninety<span style={{ color: 'var(--nn-accent-teal)' }}>Nine</span>
          </h1>
          <p style={{ fontSize: 15, color: 'var(--nn-text-secondary)', maxWidth: 440, margin: '0 0 22px', lineHeight: 1.55 }}>
            Score the classic P&amp;B pool game. Every frame, every perfect 99.
          </p>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <a href="#" className="btn btn-primary btn-lg"><Icon name="controller" /> Start new game</a>
            <a href="#" className="btn btn-outline btn-lg"><Icon name="list" /> View history</a>
          </div>
          <p style={{ margin: '18px 0 0', fontSize: 12, color: 'var(--nn-text-tertiary)' }}>Welcome back, Sam.</p>
        </div>

        {/* Decorative 9-ball */}
        <div aria-hidden style={{
          position: 'absolute', right: -40, top: '50%', transform: 'translateY(-50%)',
          width: 220, height: 220, borderRadius: '50%',
          background: 'radial-gradient(circle at 35% 30%, #fff 0%, #f9e8b6 18%, #e0b46c 45%, #8a5a1f 78%, #3d2710 100%)',
          boxShadow: '0 30px 60px rgba(0,0,0,0.5), inset -20px -30px 40px rgba(0,0,0,0.45)',
          opacity: 0.85,
        }}>
          <div style={{
            position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%,-50%)',
            width: 90, height: 90, borderRadius: '50%', background: '#fdfbf4',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            color: '#1a1208', fontSize: 46, fontWeight: 800, fontFamily: 'var(--nn-font-mono)',
            boxShadow: 'inset 0 -4px 10px rgba(0,0,0,0.15)',
          }}>9</div>
        </div>
      </section>

      {/* Friend requests */}
      <section className="nn-card nn-card--padded" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 20 }}>
          <div>
            <h2 style={{ margin: 0, fontSize: 16, fontWeight: 700, color: 'var(--nn-text-primary)', display: 'flex', alignItems: 'center', gap: 10 }}>
              Friend requests
              <span style={{
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                minWidth: 22, height: 22, padding: '0 6px',
                background: 'var(--nn-accent-teal)', color: 'var(--nn-text-on-accent)',
                borderRadius: 999, fontSize: 11, fontWeight: 700,
              }}>3</span>
            </h2>
            <p style={{ margin: '6px 0 16px', fontSize: 13, color: 'var(--nn-text-secondary)' }}>
              3 players want to add you.
            </p>
            <div style={{ display: 'flex', gap: 18 }}>
              {[
                { name: 'Jordan Cruz',   initials: 'JC', tone: 'plum'  },
                { name: 'Miguel Reyes',  initials: 'MR', tone: 'sky'   },
                { name: 'Priya Devan',   initials: 'PD', tone: 'rose'  },
              ].map(p => (
                <div key={p.name} style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <div className={`nn-avatar nn-avatar--${p.tone}`} style={{ width: 32, height: 32, fontSize: 11 }}>{p.initials}</div>
                  <span style={{ fontSize: 13, color: 'var(--nn-text-primary)', fontWeight: 500 }}>{p.name}</span>
                </div>
              ))}
            </div>
          </div>
          <a href="#" className="btn btn-primary">View all requests</a>
        </div>
      </section>

      {/* Jump back in */}
      <section style={{ marginBottom: 24 }}>
        <SectionHeader title="Jump back in" sub="Quick links to the stuff you do most." />
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {[
            { label: 'Start new game', helper: 'Pick a venue and start scoring.', icon: 'controller', tone: 'teal' },
            { label: 'View history',   helper: "Every frame you've ever played.", icon: 'list', tone: null },
            { label: 'My stats',       helper: 'Averages, bests, perfect frames.', icon: 'chartLine', tone: null },
            { label: 'Leaderboard',    helper: 'See how you stack up.', icon: 'trophyFill', tone: 'gold' },
          ].map(q => (
            <a key={q.label} href="#" className="nn-card" style={{
              padding: 18, display: 'flex', flexDirection: 'column', gap: 10,
              textDecoration: 'none', color: 'var(--nn-text-primary)',
              transition: 'border-color 120ms, transform 120ms',
            }}>
              <span style={{
                width: 36, height: 36, borderRadius: 10,
                background: q.tone === 'gold' ? 'var(--nn-accent-gold-muted)' : q.tone === 'teal' ? 'rgba(var(--nn-accent-teal-rgb),0.14)' : 'var(--nn-bg-tertiary)',
                color: q.tone === 'gold' ? 'var(--nn-accent-gold)' : q.tone === 'teal' ? 'var(--nn-accent-teal)' : 'var(--nn-text-secondary)',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
              }}><Icon name={q.icon} size={20} /></span>
              <div style={{ fontSize: 14, fontWeight: 700, color: 'var(--nn-text-primary)' }}>{q.label}</div>
              <div style={{ fontSize: 12, color: 'var(--nn-text-tertiary)', lineHeight: 1.45 }}>{q.helper}</div>
            </a>
          ))}
        </div>
      </section>

      {/* Recent activity */}
      <section>
        <SectionHeader title="Recent games" action={<a href="#" className="btn btn-ghost">View all <Icon name="caretRight" size={12} /></a>} />
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {[
            { when: '2h ago',    venue: 'Side Pocket Lounge', score: 87, status: 'Completed',  state: 'completed' },
            { when: 'Yesterday', venue: 'The Corner Shot',    score: 99, status: 'Perfect!',    state: 'perfect' },
            { when: '3d ago',    venue: 'Side Pocket Lounge', score: 42, status: 'In progress', state: 'inprogress' },
          ].map(g => (
            <a key={g.when} href="#" className="nn-card" style={{
              padding: '14px 18px', display: 'grid', gridTemplateColumns: '110px 1fr 80px 140px', alignItems: 'center', gap: 20,
              textDecoration: 'none', color: 'inherit',
            }}>
              <time style={{ fontSize: 12, color: 'var(--nn-text-tertiary)' }}>{g.when}</time>
              <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--nn-text-primary)' }}>{g.venue}</div>
              <div className="mono" style={{
                fontSize: 18, fontWeight: 700,
                color: g.state === 'perfect' ? 'var(--nn-accent-gold)' : 'var(--nn-text-primary)',
              }}>{g.score}<span style={{ color: 'var(--nn-text-tertiary)', fontSize: 13, fontWeight: 500 }}>/99</span></div>
              <span className={`nn-status-pill nn-status-pill--${g.state === 'inprogress' ? 'inprogress' : g.state === 'perfect' ? 'completed' : 'completed'}`}>{g.status}</span>
            </a>
          ))}
        </div>
      </section>
    </Shell>
  );
}

// =========================================================
// NEW GAME
// =========================================================
function NewGameScreen() {
  const sizes = [
    { size: '6 ft', sel: false },
    { size: '7 ft', sel: true },
    { size: '9 ft', sel: false },
    { size: '10 ft', sel: false },
  ];
  return (
    <Shell active="new-game">
      <div style={{ maxWidth: 620 }}>
        <h1 className="nn-page-title">Start a New Game</h1>
        <p className="nn-page-sub" style={{ marginBottom: 28 }}>
          Pick a venue and table size, then rack 'em up.
          Playing with a friend? <a href="#" style={{ color: 'var(--nn-accent-teal)' }}>Start a match instead</a>.
        </p>
        <div className="nn-card" style={{ padding: 28 }}>
          <div style={{ marginBottom: 22 }}>
            <label style={{ display: 'block', fontSize: 12, fontWeight: 700, color: 'var(--nn-text-secondary)', marginBottom: 8, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Venue</label>
            <div style={{
              position: 'relative', display: 'flex', alignItems: 'center',
              background: 'var(--nn-bg-tertiary)', border: '1px solid var(--nn-border-default)',
              borderRadius: 'var(--nn-radius-md)', padding: '10px 14px',
              color: 'var(--nn-text-primary)', fontSize: 14,
            }}>
              <Icon name="mapPin" size={16} style={{ color: 'var(--nn-text-tertiary)', marginRight: 10 }} />
              <span style={{ flex: 1 }}>Side Pocket Lounge <span style={{ color: 'var(--nn-text-tertiary)' }}>— 214 Elm St, Brooklyn</span></span>
              <Icon name="caretDown" size={14} style={{ color: 'var(--nn-text-tertiary)' }} />
            </div>
            <div style={{ fontSize: 12, color: 'var(--nn-text-tertiary)', marginTop: 8 }}>
              Venue not listed? <a href="#" style={{ color: 'var(--nn-accent-teal)' }}>Add a new venue</a>.
            </div>
          </div>

          <div style={{ marginBottom: 28 }}>
            <label style={{ display: 'block', fontSize: 12, fontWeight: 700, color: 'var(--nn-text-secondary)', marginBottom: 8, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Table size</label>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 8 }}>
              {sizes.map(s => (
                <label key={s.size} style={{
                  cursor: 'pointer',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  minHeight: 56, borderRadius: 'var(--nn-radius-md)',
                  background: s.sel ? 'rgba(var(--nn-accent-teal-rgb),0.12)' : 'var(--nn-bg-tertiary)',
                  border: s.sel ? '1.5px solid var(--nn-accent-teal)' : '1px solid var(--nn-border-default)',
                  color: s.sel ? 'var(--nn-accent-teal)' : 'var(--nn-text-primary)',
                  fontWeight: 600, fontSize: 15,
                  boxShadow: s.sel ? 'var(--nn-shadow-glow-teal)' : 'none',
                }}>{s.size}</label>
              ))}
            </div>
          </div>

          <button className="btn btn-primary btn-lg" style={{ width: '100%', justifyContent: 'center' }}>
            Start Game
          </button>
        </div>
      </div>
    </Shell>
  );
}

// =========================================================
// PLAY — live scoring. Renders the 9-column score card grid.
// =========================================================
function FrameCell({ n, bb, bc, tot, state }) {
  const dash = '\u2014';
  const isActive = state === 'active';
  const isCompleted = state === 'completed';
  const isPerfect = isCompleted && tot && (tot === n * 11);
  const borderColor = isActive ? 'var(--nn-accent-gold)' : isCompleted ? 'var(--nn-accent-teal)' : 'var(--nn-border-default)';
  const bg = isActive ? 'var(--nn-accent-gold-muted)' : isCompleted ? 'var(--nn-bg-secondary)' : 'var(--nn-bg-secondary)';
  return (
    <div style={{
      border: `1.5px solid ${borderColor}`,
      borderRadius: 'var(--nn-radius-md)',
      background: bg,
      padding: '12px 10px 14px',
      display: 'flex', flexDirection: 'column', gap: 8,
      position: 'relative',
      minHeight: 170,
      borderStyle: state === 'pending' ? 'dashed' : 'solid',
      boxShadow: isActive ? 'var(--nn-shadow-glow-gold)' : 'none',
      transform: isActive ? 'translateY(-2px)' : 'none',
    }}>
      <div style={{
        width: 28, height: 28, borderRadius: '50%',
        background: isActive ? 'var(--nn-accent-gold)' : isCompleted ? 'var(--nn-accent-teal)' : 'transparent',
        border: isCompleted || isActive ? 'none' : '1.5px solid var(--nn-border-default)',
        color: isActive ? '#1a1208' : isCompleted ? 'var(--nn-text-on-accent)' : 'var(--nn-text-tertiary)',
        fontSize: 13, fontWeight: 700,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        margin: '0 auto 4px',
        fontFamily: 'var(--nn-font-mono)',
      }}>{n}</div>
      {[
        { label: 'Break', val: bb, highlight: false },
        { label: 'Ball',  val: bc, highlight: false },
        { label: 'Total', val: tot, highlight: true },
      ].map(row => (
        <div key={row.label} style={{
          display: 'flex', justifyContent: 'space-between', alignItems: 'baseline',
          paddingTop: row.highlight ? 6 : 0,
          borderTop: row.highlight ? '1px solid var(--nn-border-subtle)' : 'none',
        }}>
          <span style={{ fontSize: 10, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--nn-text-tertiary)', fontWeight: 600 }}>{row.label}</span>
          <span className="mono" style={{
            fontSize: row.highlight ? 18 : 14,
            fontWeight: row.highlight ? 700 : 600,
            color: row.val == null ? 'var(--nn-text-tertiary)' :
                   row.highlight ? (isPerfect ? 'var(--nn-accent-gold)' : 'var(--nn-text-primary)') :
                   'var(--nn-text-primary)',
          }}>{row.val == null ? dash : row.val}</span>
        </div>
      ))}
    </div>
  );
}

function PlayScreen() {
  // Frames 1–4 completed, 5 active, 6–9 pending.
  const frames = [
    { n: 1, bb: 1, bc: 8,  tot: 9 ,  state: 'completed' },
    { n: 2, bb: 0, bc: 11, tot: 20,  state: 'completed' },
    { n: 3, bb: 1, bc: 10, tot: 31,  state: 'completed' },
    { n: 4, bb: 1, bc: 10, tot: 42,  state: 'completed' },
    { n: 5, bb: null, bc: null, tot: null, state: 'active' },
    { n: 6, bb: null, bc: null, tot: null, state: 'pending' },
    { n: 7, bb: null, bc: null, tot: null, state: 'pending' },
    { n: 8, bb: null, bc: null, tot: null, state: 'pending' },
    { n: 9, bb: null, bc: null, tot: null, state: 'pending' },
  ];
  return (
    <Shell active="new-game">
      {/* Header strip */}
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 18 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: 'var(--nn-text-secondary)', fontSize: 13 }}>
          <Icon name="mapPin" size={14} />
          <span style={{ color: 'var(--nn-text-primary)', fontWeight: 600 }}>Side Pocket Lounge</span>
          <span style={{ color: 'var(--nn-text-tertiary)' }}>·</span>
          <span>7 ft</span>
          <span style={{ color: 'var(--nn-text-tertiary)' }}>·</span>
          <time>Started Thu, 8:42 PM</time>
        </div>
        <span className="nn-status-pill nn-status-pill--inprogress">In progress</span>
      </header>

      {/* Current frame callout */}
      <div className="nn-card" style={{
        padding: '18px 22px',
        marginBottom: 18,
        borderColor: 'rgba(var(--nn-accent-gold-rgb),0.4)',
        background: 'linear-gradient(180deg, rgba(var(--nn-accent-gold-rgb),0.08) 0%, var(--nn-bg-secondary) 60%)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      }}>
        <div>
          <div style={{ fontSize: 11, color: 'var(--nn-accent-gold)', textTransform: 'uppercase', letterSpacing: '0.14em', fontWeight: 700 }}>Your turn</div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--nn-text-primary)', marginTop: 4 }}>Frame 5 of 9</div>
          <div style={{ fontSize: 13, color: 'var(--nn-text-secondary)', marginTop: 4 }}>Tap the highlighted column below to enter your score.</div>
        </div>
        <button className="btn btn-primary btn-lg"><Icon name="plus" size={16} /> Record frame 5</button>
      </div>

      {/* Score card grid (9 columns) */}
      <section style={{ display: 'grid', gridTemplateColumns: 'repeat(9, 1fr)', gap: 8, marginBottom: 18 }} aria-label="Score card grid">
        {frames.map(f => <FrameCell key={f.n} {...f} />)}
      </section>

      {/* Summary strip */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
        <StatPill label="Total"      value={42} suffix="/ 99" icon="trophy" tone="teal" />
        <StatPill label="Avg / Frame" value="10.5" icon="chartLine" />
        <StatPill label="Completed"  value={4} suffix="/ 9" icon="check" />
        <StatPill label="Current"    value={5} icon="target" tone="gold" />
      </div>

      {/* Footer */}
      <footer style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 20, fontSize: 12, color: 'var(--nn-text-tertiary)' }}>
        <span>Current frame: <strong style={{ color: 'var(--nn-text-primary)' }}>5</strong> / 9 · Score so far: <strong style={{ color: 'var(--nn-text-primary)' }}>42</strong> / 99</span>
        <a href="#" style={{ color: 'var(--nn-text-secondary)' }}>Exit to history</a>
      </footer>
    </Shell>
  );
}

// =========================================================
// COMPLETE — perfect game celebration
// =========================================================
function CompleteScreen() {
  const frames = Array.from({ length: 9 }, (_, i) => ({
    n: i + 1, bb: 1, bc: 10, tot: (i + 1) * 11, state: 'completed',
  }));
  return (
    <Shell active="history">
      <div style={{
        position: 'relative', borderRadius: 'var(--nn-radius-lg)', overflow: 'hidden',
        padding: '48px 40px 40px', marginBottom: 22,
        background: 'radial-gradient(ellipse at 50% 0%, rgba(224,180,108,0.25) 0%, rgba(20,23,25,0) 70%), var(--nn-bg-secondary)',
        border: '1px solid rgba(var(--nn-accent-gold-rgb), 0.35)',
        textAlign: 'center',
        boxShadow: 'var(--nn-shadow-glow-gold)',
      }}>
        <div style={{ fontSize: 11, color: 'var(--nn-accent-gold)', textTransform: 'uppercase', letterSpacing: '0.28em', fontWeight: 700, marginBottom: 14 }}>
            ★ ★ ★  Perfect game  ★ ★ ★
          </div>
        <div className="mono" style={{
          fontSize: 120, lineHeight: 1, fontWeight: 800,
          background: 'linear-gradient(180deg, #fef3c7, #e0b46c 55%, #8a5a1f)',
          WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent',
          letterSpacing: '-0.04em',
          textShadow: '0 2px 20px rgba(224,180,108,0.25)',
        }}>99</div>
        <div style={{ fontSize: 28, fontWeight: 700, color: 'var(--nn-text-primary)', margin: '8px 0 6px', letterSpacing: '-0.02em' }}>
          Nine for nine.
        </div>
        <div style={{ fontSize: 14, color: 'var(--nn-text-secondary)' }}>
          Thu Nov 14 · Side Pocket Lounge · 7 ft · 23 min
        </div>
        <div style={{ display: 'flex', gap: 10, justifyContent: 'center', marginTop: 24 }}>
          <button className="btn btn-primary btn-lg">Share brag card</button>
          <button className="btn btn-outline btn-lg">New game</button>
        </div>
      </div>

      {/* Completed score card (read-only) */}
      <section style={{ display: 'grid', gridTemplateColumns: 'repeat(9, 1fr)', gap: 8 }}>
        {frames.map(f => <FrameCell key={f.n} {...f} />)}
      </section>
    </Shell>
  );
}

// =========================================================
// HISTORY — /games
// =========================================================
function HistoryScreen() {
  const rows = [
    { when: '2h ago',     venue: 'Side Pocket Lounge', table: '7 ft',  score: 87, status: 'Completed',   state: 'completed',  perfect: false },
    { when: 'Yesterday',  venue: 'The Corner Shot',    table: '9 ft',  score: 99, status: 'Completed',   state: 'completed',  perfect: true  },
    { when: '3d ago',     venue: 'Side Pocket Lounge', table: '7 ft',  score: 42, status: 'In progress', state: 'inprogress', perfect: false },
    { when: '1w ago',     venue: 'Rack & Brew',         table: '9 ft',  score: 73, status: 'Completed',   state: 'completed',  perfect: false },
    { when: '2w ago',     venue: 'Side Pocket Lounge', table: '7 ft',  score: 68, status: 'Completed',   state: 'completed',  perfect: false },
    { when: '3w ago',     venue: 'The Corner Shot',    table: '9 ft',  score: 91, status: 'Completed',   state: 'completed',  perfect: false },
    { when: '1mo ago',    venue: 'Rack & Brew',         table: '9 ft',  score: 56, status: 'Completed',   state: 'completed',  perfect: false },
    { when: '1mo ago',    venue: 'Side Pocket Lounge', table: '7 ft',  score: 84, status: 'Completed',   state: 'completed',  perfect: false },
  ];
  return (
    <Shell active="history">
      <header className="nn-page-header">
        <div>
          <h1 className="nn-page-title">My Games</h1>
          <p className="nn-page-sub">46 games · Avg 74.2 · 2 perfect</p>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-ghost"><Icon name="search" size={14} /> Filter</button>
          <a href="#" className="btn btn-primary"><Icon name="plus" size={14} /> New Game</a>
        </div>
      </header>

      <div className="nn-card" style={{ overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
          <thead>
            <tr style={{ background: 'var(--nn-bg-tertiary)' }}>
              {['When', 'Venue', 'Table', 'Score', 'Status'].map(h => (
                <th key={h} style={{
                  textAlign: 'left', padding: '12px 18px',
                  fontSize: 11, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em',
                  color: 'var(--nn-text-tertiary)',
                }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={i} style={{ borderTop: '1px solid var(--nn-border-subtle)' }}>
                <td style={{ padding: '14px 18px', color: 'var(--nn-text-secondary)' }}>{r.when}</td>
                <td style={{ padding: '14px 18px', color: 'var(--nn-text-primary)', fontWeight: 600 }}>{r.venue}</td>
                <td style={{ padding: '14px 18px', color: 'var(--nn-text-secondary)' }}>{r.table}</td>
                <td className="mono" style={{ padding: '14px 18px', color: r.perfect ? 'var(--nn-accent-gold)' : 'var(--nn-text-primary)', fontWeight: 700, fontSize: 15 }}>
                  {r.score}<span style={{ color: 'var(--nn-text-tertiary)', fontWeight: 500, fontSize: 12 }}> / 99</span>
                  {r.perfect ? <span style={{
                    marginLeft: 8, padding: '1px 6px', borderRadius: 4,
                    background: 'var(--nn-accent-gold-muted)', color: 'var(--nn-accent-gold)',
                    fontSize: 10, fontWeight: 800, letterSpacing: '0.05em',
                  }}>99!</span> : null}
                </td>
                <td style={{ padding: '14px 18px' }}>
                  <span className={`nn-status-pill nn-status-pill--${r.state}`}>{r.status}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Shell>
  );
}

// =========================================================
// MY STATS — /stats/me
// =========================================================
function MyStatsScreen() {
  const distribution = [
    { bucket: '40-49', n: 3 },
    { bucket: '50-59', n: 5 },
    { bucket: '60-69', n: 9 },
    { bucket: '70-79', n: 14 },
    { bucket: '80-89', n: 11 },
    { bucket: '90-98', n: 2 },
    { bucket: '99',    n: 2 },
  ];
  const maxN = Math.max(...distribution.map(d => d.n));
  return (
    <Shell active="stats">
      <header className="nn-page-header">
        <div>
          <h1 className="nn-page-title">My Stats</h1>
          <p className="nn-page-sub">Last played 2 hours ago · Member since Mar 2024</p>
        </div>
        <button className="btn btn-outline"><Icon name="trophyFill" size={14} /> View leaderboard</button>
      </header>

      <section style={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: 12, marginBottom: 24 }}>
        <StatPill label="Total games"   value={46} icon="controller" />
        <StatPill label="Completed"     value={44} icon="checkFill" tone="teal" />
        <StatPill label="Avg score"     value="74.2" icon="chartLine" tone="teal" />
        <StatPill label="Best"          value={99} suffix="/ 99" icon="trophyFill" tone="gold" />
        <StatPill label="Perfect games" value={2}  icon="starFill" tone="gold" />
        <StatPill label="Perfect frames" value={38} icon="targetFill" tone="gold" />
      </section>

      <div style={{ display: 'grid', gridTemplateColumns: '1.5fr 1fr', gap: 16 }}>
        {/* Score distribution */}
        <div className="nn-card" style={{ padding: 22 }}>
          <SectionHeader title="Score distribution" sub="Across your 44 completed games." />
          <div style={{ display: 'flex', alignItems: 'flex-end', gap: 10, height: 170, padding: '10px 0 4px', borderBottom: '1px solid var(--nn-border-subtle)' }}>
            {distribution.map(d => {
              const isPerfect = d.bucket === '99';
              return (
                <div key={d.bucket} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8 }}>
                  <div className="mono" style={{ fontSize: 11, color: 'var(--nn-text-tertiary)' }}>{d.n}</div>
                  <div style={{
                    width: '100%',
                    height: `${(d.n / maxN) * 140}px`,
                    borderRadius: '6px 6px 0 0',
                    background: isPerfect
                      ? 'linear-gradient(180deg, var(--nn-accent-gold) 0%, var(--nn-accent-gold-muted) 100%)'
                      : 'linear-gradient(180deg, var(--nn-accent-teal) 0%, var(--nn-accent-teal-muted) 100%)',
                    boxShadow: isPerfect ? 'var(--nn-shadow-glow-gold)' : 'none',
                  }} />
                </div>
              );
            })}
          </div>
          <div style={{ display: 'flex', gap: 10, marginTop: 10 }}>
            {distribution.map(d => (
              <div key={d.bucket} style={{ flex: 1, fontSize: 10, color: 'var(--nn-text-tertiary)', textAlign: 'center', fontFamily: 'var(--nn-font-mono)' }}>{d.bucket}</div>
            ))}
          </div>
        </div>

        {/* Streaks */}
        <div className="nn-card" style={{ padding: 22 }}>
          <SectionHeader title="Streaks" />
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            {[
              { label: 'Current win streak',       value: '4', sub: 'games above 80' },
              { label: 'Longest perfect run',      value: '11', sub: 'frames in a row' },
              { label: 'Break bonuses this month', value: '17', sub: 'of 24 breaks' },
              { label: 'Favorite venue',           value: 'Side Pocket', sub: '28 games played' },
            ].map(s => (
              <div key={s.label} style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12 }}>
                <div>
                  <div style={{ fontSize: 12, color: 'var(--nn-text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{s.label}</div>
                  <div style={{ fontSize: 11, color: 'var(--nn-text-tertiary)', marginTop: 2 }}>{s.sub}</div>
                </div>
                <div className="mono" style={{ fontSize: 20, fontWeight: 700, color: 'var(--nn-text-primary)', whiteSpace: 'nowrap' }}>{s.value}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </Shell>
  );
}

// =========================================================
// LEADERBOARD — /stats
// =========================================================
function LeaderboardScreen() {
  const players = [
    { rank: 1, name: 'Priya Devan',    games: 82, avg: 81.4, best: 99, tone: 'rose' },
    { rank: 2, name: 'Miguel Reyes',   games: 71, avg: 79.1, best: 99, tone: 'sky' },
    { rank: 3, name: 'Jordan Cruz',    games: 58, avg: 76.8, best: 96, tone: 'plum' },
    { rank: 4, name: 'Sam Pocket',     games: 46, avg: 74.2, best: 99, tone: 'teal', you: true },
    { rank: 5, name: 'Cameron Liu',    games: 44, avg: 72.9, best: 94, tone: 'gold' },
    { rank: 6, name: 'Alex Kim',       games: 39, avg: 71.5, best: 93, tone: 'slate' },
    { rank: 7, name: 'Devon Hayes',    games: 33, avg: 69.8, best: 91, tone: 'rose' },
    { rank: 8, name: 'Marcus Johnson', games: 28, avg: 68.3, best: 88, tone: 'sky' },
  ];
  return (
    <Shell active="stats">
      <header className="nn-page-header">
        <div>
          <h1 className="nn-page-title">Leaderboard</h1>
          <p className="nn-page-sub">Friends only · Average score this season</p>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-ghost">This season <Icon name="caretDown" size={12} /></button>
          <button className="btn btn-ghost">Average <Icon name="caretDown" size={12} /></button>
        </div>
      </header>

      {/* Top 3 podium */}
      <section style={{ display: 'grid', gridTemplateColumns: '1fr 1.1fr 1fr', gap: 14, alignItems: 'end', marginBottom: 24 }}>
        {[players[1], players[0], players[2]].map((p, idx) => {
          const place = p.rank;
          const heights = { 1: 180, 2: 150, 3: 130 };
          const medalTone = place === 1 ? 'gold' : place === 2 ? 'slate' : 'rose';
          return (
            <div key={p.name} style={{
              padding: 20, paddingTop: 26,
              minHeight: heights[place],
              background: place === 1
                ? 'radial-gradient(ellipse at 50% 0%, rgba(224,180,108,0.22), transparent 65%), var(--nn-bg-secondary)'
                : 'var(--nn-bg-secondary)',
              border: `1px solid ${place === 1 ? 'rgba(var(--nn-accent-gold-rgb),0.35)' : 'var(--nn-border-subtle)'}`,
              borderRadius: 'var(--nn-radius-lg)',
              textAlign: 'center',
              boxShadow: place === 1 ? 'var(--nn-shadow-glow-gold)' : 'var(--nn-shadow-sm)',
            }}>
              <div className={`nn-avatar nn-avatar--${p.tone}`} style={{
                width: place === 1 ? 64 : 52, height: place === 1 ? 64 : 52,
                fontSize: place === 1 ? 20 : 16, margin: '0 auto 10px',
              }}>{p.name.split(' ').map(x=>x[0]).join('')}</div>
              <div style={{
                fontSize: 11, fontWeight: 800, letterSpacing: '0.15em',
                color: place === 1 ? 'var(--nn-accent-gold)' : 'var(--nn-text-tertiary)',
                textTransform: 'uppercase', marginBottom: 4,
              }}>
                {place === 1 ? '🏆 1st place' : place === 2 ? '2nd place' : '3rd place'}
              </div>
              <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--nn-text-primary)' }}>{p.name}</div>
              <div className="mono" style={{ fontSize: 28, fontWeight: 800, color: 'var(--nn-text-primary)', marginTop: 8, letterSpacing: '-0.02em' }}>
                {p.avg}
              </div>
              <div style={{ fontSize: 11, color: 'var(--nn-text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>avg / game</div>
            </div>
          );
        })}
      </section>

      {/* Rest of list */}
      <div className="nn-card" style={{ overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
          <thead>
            <tr style={{ background: 'var(--nn-bg-tertiary)' }}>
              {['#', 'Player', 'Games', 'Avg', 'Best'].map(h => (
                <th key={h} style={{
                  textAlign: 'left', padding: '12px 18px',
                  fontSize: 11, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em',
                  color: 'var(--nn-text-tertiary)',
                }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {players.slice(3).map(p => (
              <tr key={p.name} style={{
                borderTop: '1px solid var(--nn-border-subtle)',
                background: p.you ? 'rgba(var(--nn-accent-teal-rgb),0.06)' : 'transparent',
              }}>
                <td className="mono" style={{ padding: '14px 18px', color: 'var(--nn-text-tertiary)', fontWeight: 700, width: 52 }}>{p.rank}</td>
                <td style={{ padding: '14px 18px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <div className={`nn-avatar nn-avatar--${p.tone}`} style={{ width: 28, height: 28, fontSize: 10 }}>{p.name.split(' ').map(x=>x[0]).join('')}</div>
                    <span style={{ color: 'var(--nn-text-primary)', fontWeight: 600 }}>{p.name}</span>
                    {p.you ? <span style={{ fontSize: 10, color: 'var(--nn-accent-teal)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.08em' }}>· You</span> : null}
                  </div>
                </td>
                <td style={{ padding: '14px 18px', color: 'var(--nn-text-secondary)' }}>{p.games}</td>
                <td className="mono" style={{ padding: '14px 18px', color: 'var(--nn-text-primary)', fontWeight: 700 }}>{p.avg}</td>
                <td className="mono" style={{ padding: '14px 18px', color: p.best === 99 ? 'var(--nn-accent-gold)' : 'var(--nn-text-primary)', fontWeight: 700 }}>
                  {p.best}{p.best === 99 ? <span style={{ marginLeft: 6 }}>★</span> : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Shell>
  );
}

// =========================================================
// PLAY v2 — paper-faithful: side-by-side Break + Ball controls,
// 3x3 ball picker popover, Running Total below.
// =========================================================
function PlayScreenV2({ initialOpenFrame = null, seedActiveBalls = null } = {}) {
  // Seed data aligned with real P&B scorecard rules:
  //   breakBonus = boolean (1pt if true)
  //   balls = Set<number> of balls 1-9 pocketed this frame
  //   frame total = (bb ? 1 : 0) + balls.size
  const [frames, setFrames] = React.useState(() => ([
    { n: 1, breakBonus: true,  balls: new Set([1,2,3,4,5,6,7,8]),       state: 'completed' }, // 1+8=9
    { n: 2, breakBonus: false, balls: new Set([1,2,3,4,5,6,7,8,9]),     state: 'completed' }, // 0+9=9
    { n: 3, breakBonus: true,  balls: new Set([2,3,4,5,6,7,8,9]),       state: 'completed' }, // 1+8=9
    { n: 4, breakBonus: true,  balls: new Set([1,2,3,5,6,7,9]),         state: 'completed' }, // 1+7=8
    { n: 5, breakBonus: false, balls: new Set(seedActiveBalls || []),   state: 'active'    },
    { n: 6, breakBonus: false, balls: new Set(),                         state: 'pending'   },
    { n: 7, breakBonus: false, balls: new Set(),                         state: 'pending'   },
    { n: 8, breakBonus: false, balls: new Set(),                         state: 'pending'   },
    { n: 9, breakBonus: false, balls: new Set(),                         state: 'pending'   },
  ]));
  const [openFrame, setOpenFrame] = React.useState(initialOpenFrame);

  // Running totals: cumulative sum, only for completed/active cells that have data.
  const runningTotals = React.useMemo(() => {
    let sum = 0;
    return frames.map(f => {
      const ft = (f.breakBonus ? 1 : 0) + f.balls.size;
      if (f.state === 'pending' && ft === 0) return null;
      sum += ft;
      return f.state === 'pending' && ft === 0 ? null : sum;
    });
  }, [frames]);

  const total = runningTotals.filter(Boolean).slice(-1)[0] || 0;
  const completedCount = frames.filter(f => f.state === 'completed').length;
  const currentFrame = frames.find(f => f.state === 'active')?.n || 9;
  const avg = completedCount ? (frames.slice(0, completedCount).reduce((a, f) => a + (f.breakBonus ? 1 : 0) + f.balls.size, 0) / completedCount).toFixed(1) : '—';

  const updateFrame = (idx, patch) => {
    setFrames(prev => prev.map((f, i) => i === idx ? { ...f, ...patch } : f));
  };

  // Advance: mark current active frame as completed, make next frame active.
  const advanceFrame = () => {
    setFrames(prev => {
      const activeIdx = prev.findIndex(f => f.state === 'active');
      if (activeIdx === -1) return prev;
      return prev.map((f, i) => {
        if (i === activeIdx) return { ...f, state: 'completed' };
        if (i === activeIdx + 1) return { ...f, state: 'active' };
        return f;
      });
    });
    setOpenFrame(null);
  };

  // Close popover on outside click
  React.useEffect(() => {
    if (openFrame == null) return;
    const handler = () => setOpenFrame(null);
    // Defer so the click that opened it doesn't immediately close it.
    const t = setTimeout(() => document.addEventListener('click', handler), 0);
    return () => { clearTimeout(t); document.removeEventListener('click', handler); };
  }, [openFrame]);

  return (
    <Shell active="new-game">
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 18 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: 'var(--nn-text-secondary)', fontSize: 13 }}>
          <Icon name="mapPin" size={14} />
          <span style={{ color: 'var(--nn-text-primary)', fontWeight: 600 }}>Side Pocket Lounge</span>
          <span style={{ color: 'var(--nn-text-tertiary)' }}>·</span>
          <span>7 ft</span>
          <span style={{ color: 'var(--nn-text-tertiary)' }}>·</span>
          <time>Started Thu, 8:42 PM</time>
        </div>
        <span className="nn-status-pill nn-status-pill--inprogress">In progress</span>
      </header>

      <div className="nn-card" style={{
        padding: '18px 22px',
        marginBottom: 18,
        borderColor: 'rgba(var(--nn-accent-gold-rgb),0.4)',
        background: 'linear-gradient(180deg, rgba(var(--nn-accent-gold-rgb),0.08) 0%, var(--nn-bg-secondary) 60%)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      }}>
        <div>
          <div style={{ fontSize: 11, color: 'var(--nn-accent-gold)', textTransform: 'uppercase', letterSpacing: '0.14em', fontWeight: 700 }}>Your turn</div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--nn-text-primary)', marginTop: 4 }}>Frame {currentFrame} of 9</div>
          <div style={{ fontSize: 13, color: 'var(--nn-text-secondary)', marginTop: 4 }}>
            Tap <strong>Break</strong> if you pocketed on the break. Tap <strong>Ball</strong> to check off which balls you sank.
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 18 }}>
          <div style={{ display: 'flex', gap: 8 }}>
            <PoolBall n={currentFrame} size={54} />
            <PoolBall n={9} size={54} />
          </div>
          <button
            className="btn btn-primary btn-lg"
            onClick={() => advanceFrame()}
            style={{ whiteSpace: 'nowrap' }}
          >
            Finish frame {currentFrame} <Icon name="arrowRight" size={16} />
          </button>
        </div>
      </div>

      <section style={{ display: 'grid', gridTemplateColumns: 'repeat(9, 1fr)', gap: 8, marginBottom: 18 }} aria-label="Score card grid v2">
        {frames.map((f, i) => (
          <FrameCellV2
            key={f.n}
            n={f.n}
            breakBonus={f.breakBonus}
            balls={f.balls}
            runningTot={runningTotals[i]}
            state={f.state}
            onChange={(patch) => updateFrame(i, patch)}
            isOpen={openFrame === f.n}
            onOpen={() => setOpenFrame(f.n)}
            onClose={() => setOpenFrame(null)}
          />
        ))}
      </section>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
        <StatPill label="Total"       value={total} suffix="/ 90" icon="trophy" tone="teal" />
        <StatPill label="Avg / Frame" value={avg} icon="chartLine" />
        <StatPill label="Completed"   value={completedCount} suffix="/ 9" icon="check" />
        <StatPill label="Current"     value={currentFrame} icon="target" tone="gold" />
      </div>

      <footer style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 20, fontSize: 12, color: 'var(--nn-text-tertiary)' }}>
        <span>Max per frame: <strong style={{ color: 'var(--nn-text-primary)' }}>10</strong> (1 break bonus + 9 balls) · Max game: <strong style={{ color: 'var(--nn-text-primary)' }}>90</strong></span>
        <a href="#" style={{ color: 'var(--nn-text-secondary)' }}>Exit to history</a>
      </footer>
    </Shell>
  );
}

// =========================================================
// PLAY v2 — MULTI-PLAYER. Two stacked player rows (mirrors
// the physical P&B scorecard). Turn indicator at top shows
// whose inning it is; tapping "Finish inning" advances the
// active frame + flips to the other player.
// =========================================================
function PlayScreenV2Multi() {
  const makeFrames = (completedCount, activeIdx) => (
    Array.from({ length: 9 }, (_, i) => ({
      n: i + 1,
      breakBonus: i < completedCount ? (i % 2 === 0) : false,
      balls: i < completedCount ? new Set([1,2,3,4,5,6,7,8].slice(0, 7 + (i % 2))) : new Set(),
      state: i < completedCount ? 'completed' : i === activeIdx ? 'active' : 'pending',
    }))
  );

  const [players, setPlayers] = React.useState(() => ([
    { id: 'carey',  name: 'Carey',  frames: makeFrames(5, 5) },   // Carey already played through 5
    { id: 'george', name: 'George', frames: makeFrames(4, 4) },   // George is up now for frame 5
    { id: 'mira',   name: 'Mira',   frames: makeFrames(4, 4) },   // Mira plays after George
  ]));
  const [activePlayerId, setActivePlayerId] = React.useState('george');
  const [openCell, setOpenCell] = React.useState(null); // { playerId, frameN }

  const activeIdx = players.findIndex(p => p.id === activePlayerId);
  const activePlayer = players[activeIdx];
  // Players listed below the active one, in rotation order (next up → ... → the one who just played)
  const upNextOrder = players
    .map((p, i) => ({ p, offset: (i - activeIdx + players.length) % players.length }))
    .filter(x => x.offset !== 0)
    .sort((a, b) => a.offset - b.offset)
    .map(x => x.p);

  const runningTotalsFor = (frames) => {
    let sum = 0;
    return frames.map(f => {
      const ft = (f.breakBonus ? 1 : 0) + f.balls.size;
      if (f.state === 'pending' && ft === 0) return null;
      sum += ft;
      return sum;
    });
  };

  const updateFrame = (playerId, idx, patch) => {
    setPlayers(prev => prev.map(p =>
      p.id !== playerId ? p
      : { ...p, frames: p.frames.map((f, i) => i === idx ? { ...f, ...patch } : f) }
    ));
  };

  // Advance active player's current frame, then rotate to next player in order.
  const finishInning = () => {
    setPlayers(prev => prev.map(p => {
      if (p.id !== activePlayerId) return p;
      const activeIdx = p.frames.findIndex(f => f.state === 'active');
      if (activeIdx === -1) return p;
      return {
        ...p,
        frames: p.frames.map((f, i) => {
          if (i === activeIdx) return { ...f, state: 'completed' };
          if (i === activeIdx + 1) return { ...f, state: 'active' };
          return f;
        }),
      };
    }));
    setActivePlayerId(curId => {
      const idx = players.findIndex(p => p.id === curId);
      return players[(idx + 1) % players.length].id;
    });
    setOpenCell(null);
  };

  React.useEffect(() => {
    if (!openCell) return;
    const handler = () => setOpenCell(null);
    const t = setTimeout(() => document.addEventListener('click', handler), 0);
    return () => { clearTimeout(t); document.removeEventListener('click', handler); };
  }, [openCell]);

  const activeFrameN = activePlayer.frames.find(f => f.state === 'active')?.n || 9;

  return (
    <Shell active="new-game">
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 18 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: 'var(--nn-text-secondary)', fontSize: 13 }}>
          <Icon name="mapPin" size={14} />
          <span style={{ color: 'var(--nn-text-primary)', fontWeight: 600 }}>Side Pocket Lounge</span>
          <span style={{ color: 'var(--nn-text-tertiary)' }}>·</span>
          <span>7 ft · {players.length} players</span>
          <span style={{ color: 'var(--nn-text-tertiary)' }}>·</span>
          <time>Started Thu, 8:42 PM</time>
        </div>
        <span className="nn-status-pill nn-status-pill--inprogress">Inning {activeFrameN}</span>
      </header>

      {/* Turn indicator + advance CTA */}
      <div className="nn-card" style={{
        padding: '16px 22px',
        marginBottom: 18,
        borderColor: 'rgba(var(--nn-accent-gold-rgb),0.4)',
        background: 'linear-gradient(180deg, rgba(var(--nn-accent-gold-rgb),0.08) 0%, var(--nn-bg-secondary) 60%)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 18,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <div style={{
            width: 44, height: 44, borderRadius: '50%',
            background: 'var(--nn-accent-gold)',
            color: '#1a1208',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 18, fontWeight: 800, fontFamily: 'var(--nn-font-mono)',
            boxShadow: 'var(--nn-shadow-glow-gold)',
          }}>{activePlayer.name[0]}</div>
          <div>
            <div style={{ fontSize: 11, color: 'var(--nn-accent-gold)', textTransform: 'uppercase', letterSpacing: '0.14em', fontWeight: 700 }}>
              {activePlayer.name}'s inning
            </div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--nn-text-primary)', marginTop: 2 }}>Frame {activeFrameN} of 9</div>
            <div style={{ fontSize: 12, color: 'var(--nn-text-secondary)', marginTop: 2 }}>
              Rack {activeFrameN} ball at top · {upNextOrder[0].name} is up next
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <div style={{ display: 'flex', gap: 6 }}>
            <PoolBall n={activeFrameN} size={48} />
            <PoolBall n={9} size={48} />
          </div>
          <button
            className="btn btn-primary btn-lg"
            onClick={finishInning}
            style={{ whiteSpace: 'nowrap' }}
          >
            Finish inning <Icon name="arrowRight" size={16} />
          </button>
        </div>
      </div>

      {/* ACTIVE PLAYER — full scorecard + stat pills below */}
      {(() => {
        const p = activePlayer;
        const totals = runningTotalsFor(p.frames);
        const score = totals.filter(Boolean).slice(-1)[0] || 0;
        const completed = p.frames.filter(f => f.state === 'completed').length;
        const sumPoints = p.frames.reduce((a, f) => f.state === 'completed' ? a + (f.breakBonus ? 1 : 0) + f.balls.size : a, 0);
        const avgF = completed ? (sumPoints / completed).toFixed(1) : '—';
        return (
          <section style={{ marginBottom: 20 }} aria-label={`${p.name}'s scorecard (active)`}>
            <div style={{
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              padding: '6px 4px 10px',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <div style={{
                  width: 30, height: 30, borderRadius: '50%',
                  background: 'var(--nn-accent-gold)',
                  color: '#1a1208',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 13, fontWeight: 800, fontFamily: 'var(--nn-font-mono)',
                }}>{p.name[0]}</div>
                <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--nn-text-primary)' }}>{p.name}</div>
                <span style={{
                  fontSize: 10, fontWeight: 700, letterSpacing: '0.1em', textTransform: 'uppercase',
                  color: 'var(--nn-accent-gold)', padding: '2px 8px',
                  background: 'rgba(var(--nn-accent-gold-rgb),0.15)',
                  borderRadius: 999,
                }}>Up now</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
                <span className="mono" style={{ fontSize: 22, fontWeight: 800, color: 'var(--nn-text-primary)', letterSpacing: '-0.02em' }}>{score}</span>
                <span style={{ fontSize: 12, color: 'var(--nn-text-tertiary)' }}>/ 90</span>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(9, 1fr)', gap: 8, marginBottom: 14 }}>
              {p.frames.map((f, i) => (
                <FrameCellV2
                  key={f.n}
                  n={f.n}
                  breakBonus={f.breakBonus}
                  balls={f.balls}
                  runningTot={totals[i]}
                  state={f.state}
                  onChange={(patch) => updateFrame(p.id, i, patch)}
                  isOpen={openCell && openCell.playerId === p.id && openCell.frameN === f.n}
                  onOpen={() => setOpenCell({ playerId: p.id, frameN: f.n })}
                  onClose={() => setOpenCell(null)}
                />
              ))}
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
              <StatPill label="Total"       value={score}    suffix="/ 90" icon="trophy" tone="teal" />
              <StatPill label="Avg / Frame" value={avgF}     icon="chartLine" />
              <StatPill label="Completed"   value={completed} suffix="/ 9" icon="check" />
              <StatPill label="Current"     value={activeFrameN} icon="target" tone="gold" />
            </div>
          </section>
        );
      })()}

      {/* UP NEXT — remaining players in rotation order, compact */}
      <div style={{
        marginTop: 4, marginBottom: 10,
        display: 'flex', alignItems: 'center', gap: 10,
        fontSize: 11, textTransform: 'uppercase', letterSpacing: '0.1em',
        color: 'var(--nn-text-tertiary)', fontWeight: 700,
      }}>
        <span>Up next</span>
        <div style={{ flex: 1, height: 1, background: 'var(--nn-border-subtle)' }} />
      </div>

      {upNextOrder.map((p, orderIdx) => {
        const totals = runningTotalsFor(p.frames);
        const score = totals.filter(Boolean).slice(-1)[0] || 0;
        return (
          <section key={p.id} style={{
            marginBottom: 12,
            opacity: 0.82,
          }} aria-label={`${p.name}'s scorecard`}>
            <div style={{
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              padding: '4px 4px 6px',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <div style={{
                  width: 22, height: 22, borderRadius: '50%',
                  background: 'var(--nn-bg-tertiary)',
                  border: '1.5px solid var(--nn-border-default)',
                  color: 'var(--nn-text-secondary)',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 10, fontWeight: 800, fontFamily: 'var(--nn-font-mono)',
                }}>{p.name[0]}</div>
                <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--nn-text-primary)' }}>{p.name}</div>
                <span style={{
                  fontSize: 9, fontWeight: 700, letterSpacing: '0.1em', textTransform: 'uppercase',
                  color: 'var(--nn-text-tertiary)', padding: '1px 7px',
                  background: 'var(--nn-bg-tertiary)',
                  borderRadius: 999,
                  border: '1px solid var(--nn-border-subtle)',
                  fontFamily: 'var(--nn-font-mono)',
                }}>#{orderIdx + 2}</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
                <span className="mono" style={{ fontSize: 16, fontWeight: 700, color: 'var(--nn-text-secondary)', letterSpacing: '-0.02em' }}>{score}</span>
                <span style={{ fontSize: 11, color: 'var(--nn-text-tertiary)' }}>/ 90</span>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(9, 1fr)', gap: 6 }}>
              {p.frames.map((f, i) => (
                <FrameCellV2
                  key={f.n}
                  n={f.n}
                  breakBonus={f.breakBonus}
                  balls={f.balls}
                  runningTot={totals[i]}
                  state={f.state === 'active' ? 'pending' : f.state}
                  onChange={(patch) => updateFrame(p.id, i, patch)}
                  isOpen={openCell && openCell.playerId === p.id && openCell.frameN === f.n}
                  onOpen={() => setOpenCell({ playerId: p.id, frameN: f.n })}
                  onClose={() => setOpenCell(null)}
                />
              ))}
            </div>
          </section>
        );
      })}

      <footer style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 12, fontSize: 12, color: 'var(--nn-text-tertiary)' }}>
        <span>Innings alternate after each frame · Max game: <strong style={{ color: 'var(--nn-text-primary)' }}>90</strong></span>
        <a href="#" style={{ color: 'var(--nn-text-secondary)' }}>Exit to history</a>
      </footer>
    </Shell>
  );
}

Object.assign(window, {
  HomeScreen, NewGameScreen, PlayScreen, PlayScreenV2, PlayScreenV2Multi, CompleteScreen,
  HistoryScreen, MyStatsScreen, LeaderboardScreen,
  SectionHeader, StatPill, FrameCell,
});
