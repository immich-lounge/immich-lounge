const net = require('net');
const path = require('path');
const { randomUUID } = require('crypto');
const { spawn } = require('child_process');

// Keep the console connected before deployment, but accept only this package's run.
function runTests({
    host = process.env.ROKU_HOST,
    port = 8085,
    timeoutMs = Number(process.env.ROKU_TEST_TIMEOUT_MS || 180000),
    launchArgs = process.argv.slice(2),
    spawnDeploy = spawn,
    stdout = process.stdout,
    stderr = process.stderr
} = {}) {
    if (!host) {
        stderr.write('Missing environment variable ROKU_HOST\n');
        return Promise.resolve(1);
    }
    if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) {
        stderr.write('ROKU_TEST_TIMEOUT_MS must be a positive number.\n');
        return Promise.resolve(1);
    }

    return new Promise((resolve) => {
        const runId = randomUUID();
        const marker = `[Immich Lounge Test Run]: ${runId}`;
        let deploy;
        let deployExited = false;
        let deployExitCode = 1;
        let currentRun = false;
        let sawResult = false;
        let sawShutdown = false;
        let passed = false;
        let finished = false;
        let buffered = '';

        const timer = setTimeout(() => {
            finish(1, `Timed out after ${timeoutMs}ms waiting for Rooibos test completion.`);
        }, timeoutMs);

        const socket = net.createConnection({ host, port }, () => {
            deploy = spawnDeploy(process.execPath, [path.resolve(__dirname, 'deploy.js'), 'test', ...launchArgs], {
                cwd: path.resolve(__dirname, '..'),
                stdio: 'inherit',
                env: { ...process.env, ROKU_TEST_RUN_ID: runId }
            });
            deploy.on('exit', (code) => {
                deployExited = true;
                deployExitCode = code ?? 1;
                maybeFinish();
            });
            deploy.on('error', (error) => {
                finish(1, `Failed to start Roku test deployment: ${error.message}`);
            });
        });
        socket.setEncoding('utf8');
        socket.on('data', (chunk) => {
            stdout.write(chunk);
            buffered += chunk;
            let newlineIndex = buffered.indexOf('\n');
            while (newlineIndex >= 0 && !finished) {
                const line = buffered.slice(0, newlineIndex).replace(/\r$/, '').trim();
                buffered = buffered.slice(newlineIndex + 1);
                if (line.startsWith('[Immich Lounge Test Run]:')) {
                    currentRun = line === marker;
                    sawResult = false;
                    sawShutdown = false;
                    passed = false;
                } else if (currentRun) {
                    const result = line.match(/^\[Rooibos Result\]: (PASS|FAIL)$/);
                    if (result) {
                        sawResult = true;
                        passed = result[1] === 'PASS';
                    }
                    if (line === '[Rooibos Shutdown]') sawShutdown = true;
                }
                maybeFinish();
                newlineIndex = buffered.indexOf('\n');
            }
        });
        socket.on('error', (error) => {
            finish(1, `Failed to connect to Roku debug console at ${host}:${port}: ${error.message}`);
        });
        socket.on('close', () => {
            if (!finished && !(currentRun && sawResult && sawShutdown)) {
                finish(1, 'Roku debug console closed before test completion.');
            }
        });

        function maybeFinish() {
            if (!deployExited) return;
            if (deployExitCode !== 0) {
                finish(deployExitCode, 'Roku test deploy/launch failed.');
            } else if (currentRun && sawResult && sawShutdown) {
                finish(passed ? 0 : 1, passed ? '' : 'Rooibos reported test failures.');
            }
        }

        function finish(code, message) {
            if (finished) return;
            finished = true;
            clearTimeout(timer);
            if (message) stderr.write(`${message}\n`);
            socket.destroy();
            if (deploy && !deployExited) deploy.kill();
            resolve(code);
        }
    });
}

if (require.main === module) {
    runTests().then((code) => { process.exitCode = code; });
}

module.exports = { runTests };
