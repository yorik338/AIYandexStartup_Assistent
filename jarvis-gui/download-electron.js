const https = require('https');
const fs = require('fs');
const path = require('path');
const { Extract } = require('unzipper');

const version = '27.3.11';
const platform = 'win32';
const arch = 'x64';
const url = `https://github.com/electron/electron/releases/download/v${version}/electron-v${version}-${platform}-${arch}.zip`;

const distPath = path.join(__dirname, 'node_modules', 'electron', 'dist');
const zipPath = path.join(__dirname, 'electron.zip');

console.log('Downloading Electron', version, 'for', platform, arch);
console.log('URL:', url);
console.log('Target:', distPath);

const file = fs.createWriteStream(zipPath);

https.get(url, (response) => {
  if (response.statusCode === 302 || response.statusCode === 301) {
    // Follow redirect
    https.get(response.headers.location, (redirectResponse) => {
      redirectResponse.pipe(file);

      file.on('finish', () => {
        file.close();
        console.log('Download complete. Extracting...');

        // Remove old dist
        if (fs.existsSync(distPath)) {
          fs.rmSync(distPath, { recursive: true, force: true });
        }
        fs.mkdirSync(distPath, { recursive: true });

        // Extract zip
        fs.createReadStream(zipPath)
          .pipe(Extract({ path: distPath }))
          .on('close', () => {
            console.log('Extraction complete!');
            fs.unlinkSync(zipPath);
            console.log('Electron', version, 'installed successfully');
          });
      });
    });
  } else {
    response.pipe(file);

    file.on('finish', () => {
      file.close();
      console.log('Download complete. Extracting...');

      // Remove old dist
      if (fs.existsSync(distPath)) {
        fs.rmSync(distPath, { recursive: true, force: true });
      }
      fs.mkdirSync(distPath, { recursive: true });

      // Extract zip
      fs.createReadStream(zipPath)
        .pipe(Extract({ path: distPath }))
        .on('close', () => {
          console.log('Extraction complete!');
          fs.unlinkSync(zipPath);
          console.log('Electron', version, 'installed successfully');
        });
    });
  }
}).on('error', (err) => {
  fs.unlinkSync(zipPath);
  console.error('Download failed:', err.message);
});
