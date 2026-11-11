/* Isolated examples: values reuse QuietRoomSmoke and existing RestPanel rules. */
// Source: WorldStaminaRules.RestRecoveryPerGameHour; no game state is read.
window.REST_RULES = Object.freeze({ recoveryPerHour: 1 });
window.REST_FIXTURES = Object.freeze({
  day: Object.freeze({ date: [1, 3, 15], hour: 10, current: 40, maximum: 100, mode: 'hours' }),
  night: Object.freeze({ date: [1, 3, 15], hour: 22, current: 40, maximum: 100, mode: 'hours' }),
  date: Object.freeze({ date: [1, 12, 30], hour: 22, current: 40, maximum: 100, mode: 'date' }),
  fixed: Object.freeze({ date: [1, 3, 15], hour: 22, current: 40, maximum: 100, mode: 'hours', fixed: 8 }),
  unbound: Object.freeze({ date: null, hour: null, current: null, maximum: null, mode: 'hours' }),
  // URL-only boundary examples; they do not add toolbar actions or player data.
  full: Object.freeze({ date: [1, 3, 15], hour: 10, current: 100, maximum: 100, mode: 'full' }),
  fractional: Object.freeze({ date: [1, 3, 15], hour: 10, current: 99.2, maximum: 100, mode: 'full' })
});
