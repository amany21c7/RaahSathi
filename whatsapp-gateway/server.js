import express from 'express';
import cors from 'cors';
import qrcode from 'qrcode';
import pino from 'pino';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';
import makeWASocket, {
  DisconnectReason,
  useMultiFileAuthState,
  fetchLatestBaileysVersion
} from '@whiskeysockets/baileys';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = process.env.WA_GATEWAY_PORT || 5005;


app.use(cors());
app.use(express.json());
app.use(express.static(path.join(__dirname, 'public')));

const AUTH_FOLDER = path.join(__dirname, 'auth_session');
if (!fs.existsSync(AUTH_FOLDER)) {
  fs.mkdirSync(AUTH_FOLDER, { recursive: true });
}

let sock = null;
let currentQr = null;
let currentQrDataUrl = null;
let isConnected = false;
let connectedPhone = null;
let isInitializing = false;

const logger = pino({ level: 'silent' });

async function initWhatsApp() {
  if (isInitializing) return;
  isInitializing = true;

  try {
    const { state, saveCreds } = await useMultiFileAuthState(AUTH_FOLDER);
    const { version } = await fetchLatestBaileysVersion();

    sock = makeWASocket({
      version,
      auth: state,
      logger,
      browser: ['RaahSathi OTP Gateway', 'Chrome', '1.0.0']
    });

    sock.ev.on('creds.update', saveCreds);

    sock.ev.on('connection.update', async (update) => {
      const { connection, lastDisconnect, qr } = update;

      if (qr) {
        currentQr = qr;
        try {
          currentQrDataUrl = await qrcode.toDataURL(qr);
        } catch (err) {
          console.error('[WhatsApp Gateway] Failed to convert QR to DataURL', err);
        }
        isConnected = false;
        console.log('[WhatsApp Gateway] New QR code ready to scan.');
      }

      if (connection === 'close') {
        isConnected = false;
        currentQr = null;
        currentQrDataUrl = null;
        connectedPhone = null;
        const statusCode = lastDisconnect?.error?.output?.statusCode;
        const shouldReconnect = statusCode !== DisconnectReason.loggedOut;
        console.log(`[WhatsApp Gateway] Connection closed. Reason: ${statusCode}. Reconnecting: ${shouldReconnect}`);

        if (shouldReconnect) {
          isInitializing = false;
          setTimeout(() => initWhatsApp(), 4000);
        } else {
          console.log('[WhatsApp Gateway] Device logged out. Resetting auth session...');
          try {
            fs.rmSync(AUTH_FOLDER, { recursive: true, force: true });
            fs.mkdirSync(AUTH_FOLDER, { recursive: true });
          } catch (e) {
            console.error('Error clearing auth folder', e);
          }
          isInitializing = false;
          setTimeout(() => initWhatsApp(), 2000);
        }
      } else if (connection === 'open') {
        isConnected = true;
        currentQr = null;
        currentQrDataUrl = null;
        connectedPhone = sock?.user?.id ? sock.user.id.split(':')[0] : 'Connected';
        console.log(`[WhatsApp Gateway] ✅ WhatsApp Connected Successfully as +${connectedPhone}`);
        isInitializing = false;
      }
    });
  } catch (error) {
    console.error('[WhatsApp Gateway] Initialization error:', error);
    isInitializing = false;
    setTimeout(() => initWhatsApp(), 5000);
  }
}

// 1. Status endpoint
app.get('/status', (req, res) => {
  res.json({
    success: true,
    isConnected,
    connectedPhone,
    hasQr: Boolean(currentQrDataUrl)
  });
});

// 2. QR Code endpoint
app.get('/qr', (req, res) => {
  res.json({
    success: true,
    isConnected,
    connectedPhone,
    qrDataUrl: currentQrDataUrl
  });
});

// 3. Send OTP endpoint
app.post('/send-otp', async (req, res) => {
  const { phone, otp, message } = req.body;

  if (!phone || !otp) {
    return res.status(400).json({ success: false, message: 'Phone number and OTP are required.' });
  }

  let cleanNumber = String(phone).replace(/\D/g, '');
  // If 10 digits, prefix 91 (India)
  if (cleanNumber.length === 10) {
    cleanNumber = '91' + cleanNumber;
  }

  if (!isConnected || !sock) {
    return res.status(503).json({
      success: false,
      message: 'WhatsApp Gateway is not connected. Please scan QR code in Admin Settings.',
      isConnected: false
    });
  }

  try {
    const jid = `${cleanNumber}@s.whatsapp.net`;
    const textMessage = message || `🔐 *RaahSathi Verification Code*\n\nYour OTP is: *${otp}*\n\nValid for 5 minutes. Do not share this code with anyone.\n\n_Roadside Assistance Anywhere, Anytime - RaahSathi_`;

    await sock.sendMessage(jid, { text: textMessage });
    console.log(`[WhatsApp Gateway] ✅ OTP successfully sent to +${cleanNumber}`);

    return res.json({
      success: true,
      message: `OTP sent to +${cleanNumber} via WhatsApp.`
    });
  } catch (error) {
    console.error(`[WhatsApp Gateway] ❌ Error sending OTP to +${cleanNumber}:`, error);
    return res.status(500).json({
      success: false,
      message: `Failed to deliver WhatsApp message: ${error.message}`
    });
  }
});

// 4. Logout / Reset endpoint
app.post('/logout', async (req, res) => {
  try {
    if (sock) {
      await sock.logout();
    }
  } catch (e) {
    console.error('Error during logout:', e);
  }
  try {
    fs.rmSync(AUTH_FOLDER, { recursive: true, force: true });
    fs.mkdirSync(AUTH_FOLDER, { recursive: true });
  } catch (e) {}

  isConnected = false;
  currentQr = null;
  currentQrDataUrl = null;
  connectedPhone = null;
  isInitializing = false;

  setTimeout(() => initWhatsApp(), 1500);

  res.json({ success: true, message: 'WhatsApp session cleared. Scan new QR code.' });
});

// Start Express server and initialize WhatsApp
app.listen(PORT, '0.0.0.0', () => {
  console.log(`[WhatsApp Gateway] Server running on http://0.0.0.0:${PORT}`);
  initWhatsApp();
});

