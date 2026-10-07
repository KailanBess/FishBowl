import { KEYS, normalizeKeys, gamepadKeys } from './controls.js';
const $ = id => document.getElementById(id), sdk = globalThis.LivekitClient;
let room, host = false, invite = '', hostIdentity = '', bridge = location.hash.startsWith('#bridge=') ? location.hash.slice(8) : '', hostKey = '', approved = '', heartbeat, timer, busy = false;
const held = new Set(), encoder = new TextEncoder(), decoder = new TextDecoder();
const status = text => { $('status').textContent = text; };
function serviceUrl() {
  const url = new URL($('service').value.trim() || location.origin);
  if (url.username || url.password || (url.protocol !== 'https:' && !(url.protocol === 'http:' && ['127.0.0.1','localhost','[::1]'].includes(url.hostname)))) throw Error('Use an HTTPS token service, or localhost for development.');
  url.pathname = '/'; url.search = ''; url.hash = ''; return url.origin;
}
async function api(path, body) {
  const response = await fetch(serviceUrl() + path, { method:'POST', headers:{'Content-Type':'application/json', ...(hostKey ? {Authorization:'Bearer ' + hostKey} : {})}, body:JSON.stringify(body), signal:AbortSignal.timeout(10000), cache:'no-store', credentials:'omit' });
  const data = await response.json(); if (!response.ok) throw Error(data.error || 'Token service is unavailable.'); return data;
}
async function local(path, body) {
  if (!bridge) return;
  const response = await fetch(path, {method:'POST', headers:{Authorization:'Bearer ' + bridge,'Content-Type':'application/json'},body:JSON.stringify(body), signal:AbortSignal.timeout(2000),cache:'no-store'});
  if (!response.ok) throw Error('FishBowl session is unavailable. Reopen it inside the app.'); return response.json();
}
function guests() {
  $('guests').replaceChildren();
  if (!host || !room) return;
  for (const participant of room.remoteParticipants.values()) {
    const row = document.createElement('label'), box = document.createElement('input'); box.type = 'checkbox'; box.checked = approved === participant.identity;
    row.append(box, document.createTextNode(' Allow controls: ' + participant.identity));
    box.onchange = async () => { approved = box.checked ? participant.identity : ''; await local('/input',{keys:[]}).catch(()=>{}); guests(); }; $('guests').append(row);
  }
}
async function start(isHost) {
  if (busy || room) return; busy = true;
  try {
    host = isHost; hostKey = host ? $('hostKey').value : '';
    if (host && !bridge) throw Error('Create a host session from FishBowl. The web page can join as a guest.');
    const data = await api(host ? '/api/rooms' : '/api/token', host ? {} : {invite:$('invite').value.trim()});
    invite = host ? data.invite : $('invite').value.trim(); hostIdentity = host ? data.identity : data.host_identity;
    room = new sdk.Room({adaptiveStream:true, dynacast:true});
    room.on(sdk.RoomEvent.TrackSubscribed, (track, publication, participant) => {
      if (participant.identity !== hostIdentity) return;
      if (track.kind === sdk.Track.Kind.Video) { track.attach($('video')); $('video').muted = host; }
      else { const element = track.attach(); $('audio').append(element); element.play().catch(()=>status('Connected. Select Enable sound to hear audio.')); }
    });
    room.on(sdk.RoomEvent.TrackUnsubscribed, track => track.detach().forEach(element => { if (element !== $('video')) element.remove(); }));
    room.on(sdk.RoomEvent.ParticipantConnected, guests);
    room.on(sdk.RoomEvent.ParticipantDisconnected, participant => { if (participant.identity === approved) { approved = ''; local('/input',{keys:[]}).catch(()=>{}); } guests(); });
    room.on(sdk.RoomEvent.DataReceived, async (bytes, participant, kind, topic) => {
      if (!host || !approved || participant?.identity !== approved || topic !== 'fishbowl-controls' || bytes.length > 1024) return;
      try { const data = JSON.parse(decoder.decode(bytes)); const keys = normalizeKeys(data.keys); if (keys && data.room === room.name) await local('/input',{keys}); } catch { approved = ''; guests(); status('Remote controls paused. Check the selected game and FishBowl session.'); }
    });
    room.on(sdk.RoomEvent.Disconnected, () => { room = null; reset(); status('Disconnected. Create or join a session again.'); });
    await room.connect(data.server_url, data.participant_token);
    $('hostKey').value = ''; $('setup').hidden = true; $('session').hidden = false; $('share').hidden = !host; $('guestControls').hidden = host; $('mapping').hidden = false;
    $('invitationLabel').hidden = !host; $('invitation').value = invite; $('expires').textContent = 'Session expires ' + new Date(data.expires).toLocaleTimeString(); guests();
    status(host ? 'Connected. Choose Share game window, then approve a guest to allow controls.' : 'Connected. Waiting for the host to share a window.');
    if (host) heartbeat = setInterval(() => local('/state',{}).then(state => { if (!state.active) status('Controls paused. Enable guest controls in FishBowl and focus the selected game.'); }).catch(() => disconnect()), 1000);
    else timer = setInterval(sendControls, 150);
  } catch (error) { if (room) { await room.disconnect().catch(()=>{}); room = null; } status(error.message); reset(); }
  finally { busy = false; }
}
async function share() {
  let tracks = [];
  try {
    tracks = await room.localParticipant.createScreenTracks({audio:true, video:{displaySurface:'window'}, selfBrowserSurface:'exclude', surfaceSwitching:'exclude'});
    const video = tracks.find(track => track.kind === sdk.Track.Kind.Video);
    if (!video || video.mediaStreamTrack.getSettings().displaySurface !== 'window') { tracks.forEach(track=>track.stop()); throw Error('Choose a single game window. Entire-screen sharing is not enabled.'); }
    for (const track of tracks) await room.localParticipant.publishTrack(track, {source:track.kind === sdk.Track.Kind.Video ? sdk.Track.Source.ScreenShare : sdk.Track.Source.ScreenShareAudio});
    video.attach($('video')); $('video').muted = true;
    video.mediaStreamTrack.addEventListener('ended', () => { $('share').disabled = false; for (const track of tracks) { room?.localParticipant.unpublishTrack(track).catch(()=>{}); track.stop(); } approved = ''; local('/input',{keys:[]}).catch(()=>{}); guests(); status('Window sharing stopped.'); });
    status('Sharing the chosen window. Approve one guest below and enable controls in FishBowl.'); $('share').disabled = true;
  } catch (error) { for (const track of tracks) { room?.localParticipant.unpublishTrack(track).catch(()=>{}); track.stop(); } status(error.message || 'Window sharing was cancelled.'); }
}
async function sendControls() {
  if (!room || host) return;
  let keys = [];
  if ($('controls').checked && document.visibilityState === 'visible' && document.hasFocus()) {
    keys = [...held]; for (const pad of navigator.getGamepads?.() || []) if (pad?.connected) keys.push(...gamepadKeys(pad)); keys = [...new Set(keys)];
  }
  try { await room.localParticipant.publishData(encoder.encode(JSON.stringify({room:room.name,keys})),{reliable:true,topic:'fishbowl-controls',destinationIdentities:[hostIdentity]}); } catch { held.clear(); $('controls').checked = false; }
}
function reset() { clearInterval(heartbeat); clearInterval(timer); approved = ''; held.clear(); $('controls').checked = false; $('setup').hidden = false; $('session').hidden = true; $('share').disabled = false; $('audio').replaceChildren(); local('/input',{keys:[]}).catch(()=>{}); }
async function disconnect() {
  if (busy) return; busy = true;
  try { await sendControlsRelease(); if (host && invite) await api('/api/stop',{invite}); } catch { status('Disconnected locally. The service invitation expires automatically; stop the room from the service if needed.'); }
  finally { const previous = room; room = null; if (previous) await previous.disconnect().catch(()=>{}); reset(); hostKey = ''; invite = ''; busy = false; }
}
async function sendControlsRelease() { held.clear(); $('controls').checked = false; if (host) await local('/input',{keys:[]}); else await sendControls(); }
document.addEventListener('keydown', event => { if (!host && room && $('controls').checked && KEYS.includes(event.code) && !(event.target.tagName === 'TEXTAREA' || event.target.tagName === 'INPUT' && event.target.type !== 'checkbox')) { event.preventDefault(); held.add(event.code); sendControls(); } });
document.addEventListener('keyup', event => { held.delete(event.code); if (!host) sendControls(); });
window.addEventListener('blur', sendControlsRelease); document.addEventListener('visibilitychange', () => { if (document.hidden) sendControlsRelease(); });
window.addEventListener('pagehide', () => { held.clear(); local('/input',{keys:[]}).catch(()=>{}); room?.disconnect(); });
$('host').onclick = () => start(true); $('join').onclick = () => start(false); $('share').onclick = share; $('stop').onclick = disconnect; $('sound').onclick = () => room?.startAudio().catch(()=>status('Audio unavailable. Check the shared window audio.'));
if (bridge) {
  fetch('/config', {headers:{Authorization:'Bearer ' + bridge},cache:'no-store'}).then(response=>{if(!response.ok)throw Error();return response.json();}).then(config => { $('service').value = config.service || ''; for (const [name,value] of Object.entries(config.colors || {})) if (/^#[0-9a-f]{6}$/i.test(value)) document.documentElement.style.setProperty('--' + name,value); }).catch(()=>status('Reopen remote couch play inside FishBowl.'));
} else { $('host').hidden = true; $('hostKey').parentElement.hidden = true; $('service').value = location.origin; }
