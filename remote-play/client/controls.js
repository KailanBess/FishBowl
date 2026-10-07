// One complete pressed-key state per packet avoids lost key-up events.
export const KEYS = Object.freeze(['ArrowUp','ArrowDown','ArrowLeft','ArrowRight','KeyZ','KeyX','KeyA','KeyS','Enter','ShiftLeft','Escape','Tab']);
export function normalizeKeys(value) {
  if (!Array.isArray(value) || value.length > KEYS.length || value.some(x => !KEYS.includes(x))) return null;
  return [...new Set(value)];
}
export function gamepadKeys(pad) {
  if (!pad || pad.mapping !== 'standard') return [];
  const result = [], pressed = i => pad.buttons[i]?.pressed;
  const x = pad.axes[0] || 0, y = pad.axes[1] || 0;
  if (pressed(12) || y < -.55) result.push('ArrowUp');
  if (pressed(13) || y > .55) result.push('ArrowDown');
  if (pressed(14) || x < -.55) result.push('ArrowLeft');
  if (pressed(15) || x > .55) result.push('ArrowRight');
  ['KeyZ','KeyX','KeyA','KeyS','Tab','ShiftLeft'].forEach((key,i) => { if (pressed(i)) result.push(key); });
  if (pressed(8)) result.push('Escape'); if (pressed(9)) result.push('Enter');
  return result;
}
