const net = require('net');
const path = require('path');
const { spawn } = require('child_process');

const host = process.env.ROKU_HOST;
if (!host) {
    console.error('Missing environment variable ROKU_HOST');
    process.exit(1);
}

const timeoutMs = Number(process.env.ROKU_TEST_TIMEOUT_MS || 180000);
const launchArgs = process.argv.slice(2);
const deployScript = path.resolve(__dirname, 'deploy.js');

let deployExited = false;
let deployExitCode = 0;
let sawRooibosResult = false;
let sawRooibosShutdown = false;
let resultPassed = false;
let finished = false;
let buffered = '';

const timer = setTimeout(() => {
    finish(1, `Timed out after ${timeoutMs}ms waiting for Rooibos test completion.`);
}, timeoutMs);

const socket = net.createConnection({ host, port: 8085 }, () => {
    const deploy = spawn(process.execPath, [deployScript, 'test', ...launchArgs], {
        cwd: path.resolve(__dirname, '..'),
        stdio: 'inherit',
        env: process.env
    });

    deploy.on('exit', (code) => {
        deployExited = true;
        deployExitCode = code ?? 0;
        maybeFinish();
    });

    deploy.on('error', (error) => {
        finish(1, `Failed to start Roku test deployment: ${error.message}`);
    });
});

socket.setEncoding('utf8');

socket.on('data', (chunk) => {
    process.stdout.write(chunk);
    buffered += chunk;

    let newlineIndex = buffered.indexOf('\n');
    while (newlineIndex >= 0) {
        const rawLine = buffered.slice(0, newlineIndex);
        buffered = buffered.slice(newlineIndex + 1);
        handleLine(rawLine.replace(/\r$/, ''));
        newlineIndex = buffered.indexOf('\n');
    }
});

socket.on('error', (error) => {
    finish(1, `Failed to connect to Roku debug console at ${host}:8085: ${error.message}`);
});

socket.on('close', () => {
    maybeFinish();
});

function handleLine(line) {
    if (line.includes('[Rooibos Result]:')) {
        sawRooibosResult = true;
        resultPassed = line.includes('PASS');
    }

    if (line.includes('[Rooibos Shutdown]')) {
        sawRooibosShutdown = true;
    }

    maybeFinish();
}

function maybeFinish() {
    if (!deployExited) {
        return;
    }

    if (deployExitCode !== 0) {
        finish(deployExitCode, 'Roku test deploy/launch failed.');
        return;
    }

    if (!sawRooibosResult || !sawRooibosShutdown) {
        return;
    }

    finish(resultPassed ? 0 : 1, resultPassed ? '' : 'Rooibos reported test failures.');
}

function finish(code, message) {
    if (finished) {
        return;
    }
    finished = true;
    clearTimeout(timer);

    if (message) {
        if (code === 0) {
            console.log(message);
        } else {
            console.error(message);
        }
    }

    socket.destroy();
    process.exit(code);
}
