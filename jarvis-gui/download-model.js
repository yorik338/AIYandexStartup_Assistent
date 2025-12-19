// Script to download Vosk Russian model
const https = require('https');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const MODEL_URL = 'https://alphacephei.com/vosk/models/vosk-model-small-ru-0.22.zip';
const MODEL_NAME = 'vosk-model-small-ru-0.22';
const MODELS_DIR = path.join(__dirname, 'models');
const ZIP_PATH = path.join(MODELS_DIR, `${MODEL_NAME}.zip`);
const MODEL_PATH = path.join(MODELS_DIR, MODEL_NAME);

async function downloadFile(url, dest) {
  return new Promise((resolve, reject) => {
    console.log(`Downloading ${url}...`);
    const file = fs.createWriteStream(dest);

    const request = https.get(url, (response) => {
      if (response.statusCode === 302 || response.statusCode === 301) {
        // Follow redirect
        file.close();
        downloadFile(response.headers.location, dest).then(resolve).catch(reject);
        return;
      }

      const total = parseInt(response.headers['content-length'], 10);
      let downloaded = 0;

      response.on('data', (chunk) => {
        downloaded += chunk.length;
        const percent = ((downloaded / total) * 100).toFixed(1);
        process.stdout.write(`\rProgress: ${percent}% (${(downloaded / 1024 / 1024).toFixed(1)}MB / ${(total / 1024 / 1024).toFixed(1)}MB)`);
      });

      response.pipe(file);

      file.on('finish', () => {
        file.close();
        console.log('\nDownload complete!');
        resolve();
      });
    });

    request.on('error', (err) => {
      fs.unlink(dest, () => {});
      reject(err);
    });
  });
}

async function extractZip(zipPath, destDir) {
  console.log('Extracting model...');

  // Use PowerShell to extract on Windows
  if (process.platform === 'win32') {
    execSync(`powershell -Command "Expand-Archive -Path '${zipPath}' -DestinationPath '${destDir}' -Force"`, {
      stdio: 'inherit',
    });
  } else {
    execSync(`unzip -o "${zipPath}" -d "${destDir}"`, { stdio: 'inherit' });
  }

  console.log('Extraction complete!');
}

async function main() {
  try {
    // Create models directory
    if (!fs.existsSync(MODELS_DIR)) {
      fs.mkdirSync(MODELS_DIR, { recursive: true });
    }

    // Check if model already exists
    if (fs.existsSync(MODEL_PATH)) {
      console.log(`Model already exists at ${MODEL_PATH}`);
      return;
    }

    // Download model
    await downloadFile(MODEL_URL, ZIP_PATH);

    // Extract
    await extractZip(ZIP_PATH, MODELS_DIR);

    // Clean up zip
    fs.unlinkSync(ZIP_PATH);
    console.log('Cleaned up zip file');

    console.log(`\nModel installed at: ${MODEL_PATH}`);
    console.log('Wake word detection is ready!');
  } catch (err) {
    console.error('Error:', err.message);
    process.exit(1);
  }
}

main();
