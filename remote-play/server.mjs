import http from 'node:http';
import { randomBytes, timingSafeEqual } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { AccessToken, RoomServiceClient, TrackSource } from 'livekit-server-sdk';

const secret = () => randomBytes(24).toString('base64url');
const equal = (a, b) => typeof a === 'string' && a.length === b.length && timingSafeEqual(Buffer.from(a), Buffer.from(b));
export function validateConfig(env) {
  const url = new URL(env.LIVEKIT_URL || '');
  if (url.username || url.password || (url.protocol !== 'wss:' && !(url.protocol === 'ws:' && ['127.0.0.1', 'localhost', '[::1]'].includes(url.hostname)))) throw Error('LIVEKIT_URL must use WSS, or WS on localhost for development.');
  if (!env.LIVEKIT_API_KEY || !env.LIVEKIT_API_SECRET || (env.FISHBOWL_HOST_KEY || '').length < 32) throw Error('Set LIVEKIT_API_KEY, LIVEKIT_API_SECRET and a random FISHBOWL_HOST_KEY of at least 32 characters.');
  return { url: url.toString(), key: env.LIVEKIT_API_KEY, apiSecret: env.LIVEKIT_API_SECRET, hostKey: env.FISHBOWL_HOST_KEY };
}
export function createService(config, options = {}) {
  const now = options.now || Date.now;
  const rooms = new Map(), limits = new Map();
  const roomService = options.roomService || new RoomServiceClient(config.url.replace(/^ws/, 'http'), config.key, config.apiSecret);
  const token = async (room, identity, host) => {
    const access = new AccessToken(config.key, config.apiSecret, { identity, ttl: 300 });
    access.addGrant({ roomJoin: true, room, canSubscribe: true, canPublish: host, canPublishSources: host ? [TrackSource.SCREEN_SHARE, TrackSource.SCREEN_SHARE_AUDIO] : [], canPublishData: true });
    return access.toJwt();
  };
  const hostAuthorized = req => equal(req.headers.authorization || '', 'Bearer ' + config.hostKey);
  const allowedOrigin = value => {
    if (!value) return true;
    try { const url = new URL(value); return /^https?:$/.test(url.protocol) && ['127.0.0.1', 'localhost', '[::1]'].includes(url.hostname) || value === config.publicOrigin; } catch { return false; }
  };
  async function body(req) {
    if (!(req.headers['content-type'] || '').startsWith('application/json')) throw Error('JSON required.');
    let data = ''; for await (const chunk of req) { data += chunk; if (data.length > 4096) throw Error('Request exceeds limit.'); }
    return JSON.parse(data);
  }
  function send(res, status, data, type = 'application/json; charset=utf-8') {
    res.writeHead(status, { 'Content-Type': type, 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff', 'Referrer-Policy': 'no-referrer', 'Content-Security-Policy': "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self' https: wss: http://127.0.0.1:* ws://127.0.0.1:*; media-src blob:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'" });
    res.end(type.startsWith('application/json') ? JSON.stringify(data) : data);
  }
  async function cleanup() {
    for (const [code, room] of rooms) if (room.expires <= now()) {
      try { await roomService.deleteRoom(room.name); rooms.delete(code); } catch (error) { if (error.status === 404 || error.code === 'not_found') rooms.delete(code); /* Other failures retain the expired tombstone for retry. */ }
    }
    for (const [ip, limit] of limits) if (limit.expires <= now()) limits.delete(ip);
  }
  const timer = setInterval(() => cleanup().catch(() => {}), 30000); timer.unref();
  const server = http.createServer(async (req, res) => {
    try {
      if (!allowedOrigin(req.headers.origin)) return send(res, 403, { error: 'Origin is not allowed.' });
      if (req.headers.origin) { res.setHeader('Access-Control-Allow-Origin', req.headers.origin); res.setHeader('Vary', 'Origin'); }
      if (req.method === 'OPTIONS') { res.setHeader('Access-Control-Allow-Headers', 'Authorization, Content-Type'); res.setHeader('Access-Control-Allow-Methods', 'GET, POST'); return send(res, 204, ''); }
      if (req.method === 'GET') {
        const paths = { '/': 'client/index.html', '/client.js': 'client/client.js', '/controls.js': 'client/controls.js', '/style.css': 'client/style.css', '/livekit.js': 'client/livekit.js' };
        if (!paths[req.url]) return send(res, 404, { error: 'Page not found.' });
        const bytes = await readFile(new URL(paths[req.url], import.meta.url));
        const type = req.url === '/' ? 'text/html; charset=utf-8' : req.url.endsWith('.css') ? 'text/css; charset=utf-8' : 'text/javascript; charset=utf-8';
        return send(res, 200, bytes, type);
      }
      if (req.method !== 'POST') return send(res, 405, { error: 'Use POST.' });
      const ip = req.socket.remoteAddress;
      const limit = limits.get(ip) || { count: 0, expires: now() + 60000 };
      if (limit.expires <= now()) { limit.count = 0; limit.expires = now() + 60000; }
      limits.set(ip, limit);
      if (++limit.count > 30 || limits.size > 10000) return send(res, 429, { error: 'Too many requests. Try again shortly.' });
      const data = await body(req);
      if (req.url === '/api/rooms') {
        if (!hostAuthorized(req)) return send(res, 401, { error: 'Host key is required.' });
        if (rooms.size >= 100) return send(res, 503, { error: 'Room limit reached.' });
        const code = secret(), name = 'fishbowl-' + secret(), identity = 'host-' + secret();
        await roomService.createRoom({ name, maxParticipants: 5, emptyTimeout: 60, departureTimeout: 60 });
        const record = { name, identity, expires: now() + 2 * 60 * 60 * 1000 }; rooms.set(code, record);
        return send(res, 201, { server_url: config.url, participant_token: await token(name, identity, true), identity, room_name: name, invite: code, expires: record.expires });
      }
      const record = typeof data.invite === 'string' && rooms.get(data.invite);
      if (!record || record.expires <= now()) return send(res, 403, { error: 'Invitation expired or unavailable.' });
      if (req.url === '/api/token') {
        const identity = 'guest-' + secret();
        return send(res, 201, { server_url: config.url, participant_token: await token(record.name, identity, false), identity, room_name: record.name, host_identity: record.identity, expires: record.expires });
      }
      if (req.url === '/api/stop') {
        if (!hostAuthorized(req)) return send(res, 401, { error: 'Host key is required.' });
        // Deleting the room disconnects existing participants, not merely future joins.
        try { await roomService.deleteRoom(record.name); } catch (error) { if (error.status !== 404 && error.code !== 'not_found') throw error; } rooms.delete(data.invite);
        return send(res, 200, { stopped: true });
      }
      return send(res, 404, { error: 'Endpoint not found.' });
    } catch { if (!res.headersSent) send(res, 400, { error: 'Request could not be completed. Check the service configuration.' }); else res.end(); }
  });
  server.requestTimeout = 10000; server.headersTimeout = 10000;
  server.on('close', () => clearInterval(timer));
  return { server, cleanup, rooms };
}
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const config = validateConfig(process.env);
  config.publicOrigin = process.env.FISHBOWL_PUBLIC_ORIGIN || '';
  const { server } = createService(config);
  server.listen(Number(process.env.PORT || 8787), process.env.BIND_ADDRESS || '127.0.0.1', () => console.log('FishBowl token service is listening. Configure an HTTPS proxy before sharing outside localhost.'));
}
