namespace NinetyNine.Model;

/// <summary>
/// Identifies a distinct rating-bearing pool-game discipline. The
/// per-player rating system tracks an independent rating for each
/// discipline a player participates in; a player who's a 75 in
/// <see cref="Standard99"/> may be a 65 in <see cref="Efren99"/> (the
/// no-ball-in-hand variant is harder).
///
/// <para>
/// v0.9.0 ships only the two NinetyNine variants — they're derived
/// from <see cref="Game.IsEfrenVariant"/> at the service layer, NOT
/// stored as a field on <c>Game</c>. When the third discipline is
/// added (8-ball, 9-ball, etc.), <c>Game</c> gains a
/// <c>Discipline</c> field and the derivation goes away. Adding a
/// field speculatively now would be premature — there's only one
/// boolean to derive from.
/// </para>
/// </summary>
public enum GameDiscipline
{
    /// <summary>
    /// NinetyNine standard rules: ball-in-hand placement after the
    /// break. The default scoring discipline; what most players play.
    /// </summary>
    Standard99 = 0,

    /// <summary>
    /// NinetyNine "Efren" variant: no ball-in-hand placement after
    /// the break — the player runs from where the cue ball stops.
    /// Same scoring math; harder. Named for Efren Reyes.
    /// </summary>
    Efren99 = 1,

    // Future disciplines (8-ball, 9-ball, 10-ball, one-pocket, bank,
    // straight pool, etc.) get appended here as the app expands. Each
    // gets its own independent rating per player.
}
