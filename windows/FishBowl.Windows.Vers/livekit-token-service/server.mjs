import crypto from 'node:crypto';
import express from 'express';
import { AccessToken } from 'livekit-server-sdk';

const required = ['LIVEKIT_URL', 'LIVEKIT_API_KEY', 'LIVEKIT_API_SECRET', 'FISHBOWL_PAIRING_SECRET'];
for (const key of required) {
  if (!process.env[key]) throw new Error(`Missing required environment variable: ${key}`);
}

const app = express();
app.disable('x-powered-by');
app.use(express.json({ limit: '8kb' }));

function isSafeId(value) {
  return typeof value === 'string' && /^[a-zA-Z0-9_-]{8,64}$/.test(value);
}

function pairingIsValid(value) {
  if (typeof value !== 'string') return false;
  const expected = Buffer.from(process.env.FISHBOWL_PAIRING_SECRET);
  const received = Buffer.from(value);
  return received.length === expected.length && crypto.timingSafeEqual(received, expected);
}

app.get('/health', (request, response) => response.status(200).json({ status: 'ok' }));

// Standard LiveKit token-endpoint response. Keep account credentials on this server only.
app.post('/token', async (request, response) => {
  if (!pairingIsValid(request.get('x-fishbowl-pairing'))) {
    return response.status(401).json({ error: 'A valid pairing secret is required.' });
  }

  const { room_name: roomName, participant_identity: identity, participant_name: name } = request.body || {};
  if (!isSafeId(roomName) || !isSafeId(identity)) {
    return response.status(400).json({ error: 'room_name and participant_identity must be opaque 8-64 character IDs.' });
  }

  const token = new AccessToken(process.env.LIVEKIT_API_KEY, process.env.LIVEKIT_API_SECRET, {
    identity,
    name: typeof name === 'string' ? name.slice(0, 64) : undefined,
    ttl: '15m'
  });
  token.addGrant({ roomJoin: true, room: roomName, canPublish: true, canSubscribe: true, canPublishData: true });

  return response.status(201).json({
    server_url: process.env.LIVEKIT_URL,
    participant_token: await token.toJwt()
  });
});

app.use((error, request, response, next) => {
  if (error instanceof SyntaxError) return response.status(400).json({ error: 'Invalid JSON.' });
  console.error(error);
  return response.status(500).json({ error: 'Token service could not create a session token.' });
});

app.listen(process.env.PORT || 3000, () => console.log('FishBowl LiveKit token service is running.'));
