// FrameCellV2.jsx — translated-from-paper frame cell.
//
// Layout mirrors the physical P&B scorecard (reference/scoresheet-photo-*.jpeg):
//
//   ┌──────────────────┐
//   │  #  (frame num)  │
//   ├─────────┬────────┤
//   │ Break   │  Ball  │   ← two side-by-side controls
//   │ ☐ 1pt   │   6    │   ← break = checkbox, ball = opens 3x3 picker
//   ├─────────┴────────┤
//   │   Running: 28    │   ← running total below both controls
//   └──────────────────┘
//
// Clicking "Ball Count" pops up a 3x3 multi-check grid of the 9 numbered
// pool balls. Each check adds that ball to the count; closing the popover
// collapses the cell back to the summed number.
//
// Renders standalone — doesn't depend on Shell/theme chrome except for the
// --nn-* tokens, which are defined in mockups/theme.css.

// ---------------------------------------------------------------
// PoolBall — CSS/SVG rendering of a standard P&B ball.
// size = diameter in px. The 9-ball gets a horizontal stripe band
// (same base color as the 1, per standard billiards convention).
// ---------------------------------------------------------------
const BALL_COLORS = {
  1: '#e5c107', // yellow
  2: '#0a3a8c', // blue
  3: '#b8211b', // red
  4: '#4a1d72', // purple
  5: '#d66616', // orange
  6: '#0b6b3a', // green
  7: '#6b1f18', // maroon
  8: '#0f0f0f', // black
  9: '#e5c107', // yellow (with white stripe)
};

function PoolBall({ n, size = 36, dim = false, striped = null }) {
  const color = BALL_COLORS[n];
  const isStripe = striped == null ? n >= 9 : striped;  // 9 is striped in P&B
  const id = `bgrad-${n}`;
  const hid = `hl-${n}`;
  return (
    <svg
      width={size} height={size}
      viewBox="0 0 40 40"
      style={{
        filter: dim ? 'grayscale(0.7) opacity(0.45)' : 'none',
        transition: 'filter 120ms',
        flexShrink: 0,
      }}
      aria-label={`${n} ball`}
    >
      <defs>
        {/* 3-d shading: highlight upper-left, shadow lower-right */}
        <radialGradient id={id} cx="35%" cy="32%" r="70%">
          <stop offset="0%" stopColor="#fff" stopOpacity="0.55" />
          <stop offset="18%" stopColor="#fff" stopOpacity="0.25" />
          <stop offset="55%" stopColor={color} stopOpacity="1" />
          <stop offset="100%" stopColor="#000" stopOpacity="0.45" />
        </radialGradient>
        <radialGradient id={hid} cx="32%" cy="28%" r="18%">
          <stop offset="0%" stopColor="#fff" stopOpacity="0.95" />
          <stop offset="100%" stopColor="#fff" stopOpacity="0" />
        </radialGradient>
      </defs>

      {/* base */}
      {isStripe ? (
        <>
          <circle cx="20" cy="20" r="19" fill="#f8f5ec" />
          {/* horizontal stripe band */}
          <rect x="1" y="13" width="38" height="14" fill={color} />
          {/* apply shading on top */}
          <circle cx="20" cy="20" r="19" fill={`url(#${id})`} fillOpacity="0.55" />
        </>
      ) : (
        <circle cx="20" cy="20" r="19" fill={`url(#${id})`} />
      )}

      {/* number disc */}
      <circle cx="20" cy="20" r="8.5" fill="#fdfbf4" />
      <text
        x="20" y="20"
        textAnchor="middle"
        dominantBaseline="central"
        style={{
          font: `700 11px/1 ui-monospace, "SF Mono", Menlo, monospace`,
          fill: '#1a1208',
          letterSpacing: '-0.02em',
        }}
      >{n}</text>

      {/* top-left specular highlight */}
      <circle cx="13" cy="12" r="4" fill={`url(#${hid})`} />
    </svg>
  );
}

// ---------------------------------------------------------------
// BallPicker — 3x3 multi-check popover (balls 1-9).
// Rules for which balls are selectable:
//   • the "inning ball" (ball N for frame N) and the 9 are always racked
//   • all 9 balls are available to pocket in any inning; the user just
//     checks off which ones got pocketed this frame
// For the mockup we allow all 9 to be toggled.
// ---------------------------------------------------------------
function BallPicker({ selected, onToggle, onClose, onClear }) {
  return (
    <div
      style={{
        position: 'absolute',
        top: '100%',
        left: '50%',
        transform: 'translate(-50%, 8px)',
        zIndex: 20,
        width: 224,
        background: 'var(--nn-bg-tertiary)',
        border: '1px solid var(--nn-border-default)',
        borderRadius: 'var(--nn-radius-lg)',
        boxShadow: 'var(--nn-shadow-lg)',
        padding: 14,
      }}
      onClick={(e) => e.stopPropagation()}
    >
      {/* Caret */}
      <div style={{
        position: 'absolute',
        top: -7, left: '50%', transform: 'translateX(-50%) rotate(45deg)',
        width: 12, height: 12,
        background: 'var(--nn-bg-tertiary)',
        borderTop: '1px solid var(--nn-border-default)',
        borderLeft: '1px solid var(--nn-border-default)',
      }} />

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
        <div style={{ fontSize: 11, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--nn-text-tertiary)' }}>
          Balls pocketed
        </div>
        <button
          onClick={onClear}
          style={{
            background: 'transparent', border: 'none', color: 'var(--nn-text-tertiary)',
            fontSize: 11, fontWeight: 600, cursor: 'pointer', padding: '2px 6px',
            borderRadius: 4, textTransform: 'uppercase', letterSpacing: '0.05em',
          }}
        >Clear</button>
      </div>

      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: 8,
        marginBottom: 12,
      }}>
        {[1,2,3,4,5,6,7,8,9].map(n => {
          const isSelected = selected.has(n);
          return (
            <button
              key={n}
              onClick={() => onToggle(n)}
              style={{
                position: 'relative',
                padding: 6,
                background: isSelected ? 'rgba(var(--nn-accent-teal-rgb),0.12)' : 'transparent',
                border: `1.5px solid ${isSelected ? 'var(--nn-accent-teal)' : 'var(--nn-border-subtle)'}`,
                borderRadius: 'var(--nn-radius-md)',
                cursor: 'pointer',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                transition: 'all 120ms',
                aspectRatio: '1 / 1',
              }}
              aria-pressed={isSelected}
              aria-label={`Ball ${n} ${isSelected ? 'pocketed' : 'not pocketed'}`}
            >
              <PoolBall n={n} size={44} dim={!isSelected} />
              {isSelected && (
                <span style={{
                  position: 'absolute', top: 3, right: 3,
                  width: 14, height: 14, borderRadius: '50%',
                  background: 'var(--nn-accent-teal)',
                  color: 'var(--nn-text-on-accent)',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 9, fontWeight: 800,
                  boxShadow: '0 1px 3px rgba(0,0,0,0.4)',
                }}>✓</span>
              )}
            </button>
          );
        })}
      </div>

      <button
        onClick={onClose}
        className="btn btn-primary"
        style={{ width: '100%', justifyContent: 'center' }}
      >Done · {selected.size} ball{selected.size === 1 ? '' : 's'}</button>
    </div>
  );
}

// ---------------------------------------------------------------
// FrameCellV2 — the updated cell with side-by-side controls.
// Props:
//   n          — frame number 1-9
//   breakBonus — boolean (true = +1 break bonus awarded)
//   balls      — Set<number> of balls pocketed this frame (0-9 members)
//   runningTot — cumulative running total after this frame (shown read-only)
//   state      — 'completed' | 'active' | 'pending'
//   onChange   — (patch) => void   patch = { breakBonus?, balls? }
//   isOpen     — boolean (is the picker popover open for this cell)
//   onOpen / onClose — picker control handlers
// ---------------------------------------------------------------
function FrameCellV2({ n, breakBonus, balls, runningTot, state, onChange, isOpen, onOpen, onClose }) {
  const isActive = state === 'active';
  const isCompleted = state === 'completed';
  const isPending = state === 'pending';

  const ballCount = balls ? balls.size : 0;
  const frameTotal = (breakBonus ? 1 : 0) + ballCount;
  const isPerfect = isCompleted && frameTotal === 10; // 1 break + 9 balls = 10 max per frame under this rule

  const borderColor = isActive ? 'var(--nn-accent-gold)' : isCompleted ? 'var(--nn-accent-teal)' : 'var(--nn-border-default)';
  const borderStyle = isPending ? 'dashed' : 'solid';
  const shadow = isActive ? 'var(--nn-shadow-glow-gold)' : isOpen ? 'var(--nn-shadow-md)' : 'var(--nn-shadow-sm)';

  const toggleBreak = () => {
    if (isPending) return;
    onChange({ breakBonus: !breakBonus });
  };
  const toggleBall = (ballN) => {
    if (isPending) return;
    const next = new Set(balls || []);
    next.has(ballN) ? next.delete(ballN) : next.add(ballN);
    onChange({ balls: next });
  };
  const clearBalls = () => onChange({ balls: new Set() });

  return (
    <div
      style={{
        position: 'relative',
        border: `1.5px solid ${borderColor}`,
        borderStyle,
        borderRadius: 'var(--nn-radius-md)',
        background: isActive ? 'var(--nn-accent-gold-muted)' : 'var(--nn-bg-secondary)',
        boxShadow: shadow,
        padding: '8px 8px 10px',
        display: 'flex', flexDirection: 'column', gap: 6,
        minHeight: 146,
        transform: isActive ? 'translateY(-2px)' : 'none',
        transition: 'transform 140ms, box-shadow 140ms',
        opacity: isPending ? 0.6 : 1,
      }}
    >
      {/* Frame number badge */}
      <div style={{ display: 'flex', justifyContent: 'center' }}>
        <div style={{
          width: 22, height: 22, borderRadius: '50%',
          background: isActive ? 'var(--nn-accent-gold)' : isCompleted ? 'var(--nn-accent-teal)' : 'transparent',
          border: isCompleted || isActive ? 'none' : '1.5px solid var(--nn-border-default)',
          color: isActive ? '#1a1208' : isCompleted ? 'var(--nn-text-on-accent)' : 'var(--nn-text-tertiary)',
          fontSize: 12, fontWeight: 800,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontFamily: 'var(--nn-font-mono)',
        }}>{n}</div>
      </div>

      {/* Side-by-side controls: Break | Ball */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: 6,
      }}>
        {/* Break Bonus — checkbox */}
        <button
          onClick={toggleBreak}
          disabled={isPending}
          style={{
            display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 3,
            padding: '5px 4px',
            background: breakBonus ? 'rgba(var(--nn-accent-teal-rgb),0.12)' : 'var(--nn-bg-tertiary)',
            border: `1.5px solid ${breakBonus ? 'var(--nn-accent-teal)' : 'var(--nn-border-subtle)'}`,
            borderRadius: 'var(--nn-radius-sm)',
            cursor: isPending ? 'not-allowed' : 'pointer',
            transition: 'all 120ms',
            fontFamily: 'inherit',
          }}
          aria-pressed={!!breakBonus}
          aria-label="Break bonus"
        >
          <span style={{
            fontSize: 9, fontWeight: 700, letterSpacing: '0.05em', textTransform: 'uppercase',
            color: 'var(--nn-text-tertiary)', lineHeight: 1,
          }}>Break</span>
          <span style={{
            width: 20, height: 20,
            borderRadius: 5,
            border: `1.5px solid ${breakBonus ? 'var(--nn-accent-teal)' : 'var(--nn-border-strong)'}`,
            background: breakBonus ? 'var(--nn-accent-teal)' : 'transparent',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            transition: 'all 120ms',
          }}>
            {breakBonus && (
              <svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="#0a1512" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M2.5 6L5 8.5L9.5 3.5" />
              </svg>
            )}
          </span>
        </button>

        {/* Ball Count — opens picker */}
        <button
          onClick={isOpen ? onClose : onOpen}
          disabled={isPending}
          style={{
            display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 3,
            padding: '5px 4px',
            background: ballCount > 0 ? 'rgba(var(--nn-accent-teal-rgb),0.12)' : 'var(--nn-bg-tertiary)',
            border: `1.5px solid ${isOpen ? 'var(--nn-accent-teal)' : ballCount > 0 ? 'var(--nn-accent-teal)' : 'var(--nn-border-subtle)'}`,
            borderRadius: 'var(--nn-radius-sm)',
            cursor: isPending ? 'not-allowed' : 'pointer',
            transition: 'all 120ms',
            fontFamily: 'inherit',
          }}
          aria-expanded={isOpen}
          aria-label="Ball count"
        >
          <span style={{
            fontSize: 9, fontWeight: 700, letterSpacing: '0.05em', textTransform: 'uppercase',
            color: 'var(--nn-text-tertiary)', lineHeight: 1,
          }}>Ball</span>
          <span className="mono" style={{
            fontSize: 18, fontWeight: 700,
            color: ballCount > 0 ? 'var(--nn-accent-teal)' : 'var(--nn-text-tertiary)',
            lineHeight: 1,
          }}>{ballCount || '—'}</span>
        </button>
      </div>

      {/* Running total — larger, centered, no label (self-explaining) */}
      <div style={{
        marginTop: 'auto',
        paddingTop: 6,
        borderTop: '1px solid var(--nn-border-subtle)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        minHeight: 40,
      }}>
        <span className="mono" style={{
          fontSize: 30, fontWeight: 800,
          color: runningTot == null ? 'var(--nn-text-tertiary)'
               : isPerfect ? 'var(--nn-accent-gold)'
               : 'var(--nn-text-primary)',
          letterSpacing: '-0.03em',
          lineHeight: 1,
        }}>
          {runningTot == null ? '—' : runningTot}
        </span>
      </div>

      {/* Picker popover */}
      {isOpen && (
        <BallPicker
          selected={balls || new Set()}
          onToggle={toggleBall}
          onClose={onClose}
          onClear={clearBalls}
        />
      )}
    </div>
  );
}

Object.assign(window, { PoolBall, BallPicker, FrameCellV2, BALL_COLORS });
