const assert = require('node:assert/strict');
const net = require('node:net');
const { EventEmitter } = require('node:events');
const { once } = require('node:events');
const { test } = require('node:test');
const { runTests } = require('./run-tests');

const result = (status) => `[Rooibos Result]: ${status}\r\n[Rooibos Shutdown]\r\n`;

async function simulate(t, scenario, timeoutMs = 1000) {
    const server = net.createServer();
    server.listen(0, '127.0.0.1');
    await once(server, 'listening');
    let output = '';
    let errors = '';
    let killed = false;
    let settled = false;
    let child;
    let runId;
    let ready;
    const deployed = new Promise((resolve) => { ready = resolve; });
    const connection = once(server, 'connection');
    const completion = runTests({
        host: '127.0.0.1',
        port: server.address().port,
        timeoutMs,
        stdout: { write(chunk) { output += chunk; } },
        stderr: { write(chunk) { errors += chunk; } },
        spawnDeploy(command, args, options) {
            assert.equal(args[1], 'test');
            runId = options.env.ROKU_TEST_RUN_ID;
            assert.match(runId, /^[0-9a-f-]{36}$/);
            child = new EventEmitter();
            child.kill = () => { killed = true; };
            ready();
            return child;
        }
    }).then((code) => { settled = true; return code; });
    const [socket] = await connection;
    t.after(() => { socket.destroy(); server.close(); });
    await deployed;
    const marker = `[Immich Lounge Test Run]: ${runId}\r\n`;
    const waitForOutput = async (text) => {
        const limit = Date.now() + 500;
        while (!output.includes(text)) {
            assert.ok(Date.now() < limit, `Missing console output: ${text}`);
            await new Promise((resolve) => setTimeout(resolve, 5));
        }
    };
    await scenario({ socket, child, marker, waitForOutput, isSettled: () => settled });
    const code = await completion;
    return { code, errors, killed };
}

test('ignores buffered PASS and accepts a fresh FAIL', async (t) => {
    const actual = await simulate(t, async ({ socket, child, marker, waitForOutput, isSettled }) => {
        socket.write(result('PASS'));
        child.emit('exit', 0);
        await waitForOutput('[Rooibos Shutdown]');
        assert.equal(isSettled(), false);
        socket.write(marker + result('FAIL'));
    });
    assert.equal(actual.code, 1);
    assert.match(actual.errors, /test failures/);
});

test('ignores buffered FAIL and another run ID; handles split markers and lines', async (t) => {
    const actual = await simulate(t, async ({ socket, child, marker, waitForOutput }) => {
        socket.write(result('FAIL') + '[Immich Lounge Test Run]: older-run\r\n' + result('FAIL'));
        socket.write(marker.slice(0, 12));
        await waitForOutput('older-run');
        socket.write(marker.slice(12) + '[Rooibos Res');
        socket.write('ult]: PASS\r\n[Rooibos Shutdown]\r\n');
        child.emit('exit', 0);
    });
    assert.equal(actual.code, 0);
    assert.equal(actual.errors, '');
});

test('retains fresh completion before deployment exits, even if console closes', async (t) => {
    const actual = await simulate(t, async ({ socket, child, marker, waitForOutput, isSettled }) => {
        const closed = once(socket, 'close');
        socket.end(marker + result('PASS'));
        await waitForOutput('[Rooibos Shutdown]');
        await closed;
        assert.equal(isSettled(), false);
        child.emit('exit', 0);
    });
    assert.equal(actual.code, 0);
});

test('fresh PASS cannot hide a deployment failure', async (t) => {
    const actual = await simulate(t, async ({ socket, child, marker, waitForOutput }) => {
        socket.write(marker + result('PASS'));
        await waitForOutput('[Rooibos Shutdown]');
        child.emit('exit', 7);
    });
    assert.equal(actual.code, 7);
    assert.match(actual.errors, /deploy\/launch failed/);
});

test('a deployment terminated by a signal fails', async (t) => {
    const actual = await simulate(t, async ({ child }) => { child.emit('exit', null, 'SIGTERM'); });
    assert.equal(actual.code, 1);
});

test('old completion without a matching run times out', async (t) => {
    const actual = await simulate(t, async ({ socket, child }) => {
        socket.write(result('PASS'));
        child.emit('exit', 0);
    }, 500);
    assert.equal(actual.code, 1);
    assert.match(actual.errors, /Timed out/);
});

test('a result without shutdown times out and stops an unfinished deploy', async (t) => {
    const actual = await simulate(t, async ({ socket, marker }) => {
        socket.write(marker + '[Rooibos Result]: PASS\r\n');
    }, 500);
    assert.equal(actual.code, 1);
    assert.equal(actual.killed, true);
});

test('console disconnect before completion fails', async (t) => {
    const actual = await simulate(t, async ({ socket, child, marker }) => {
        child.emit('exit', 0);
        socket.end(marker);
    });
    assert.equal(actual.code, 1);
    assert.match(actual.errors, /closed before/);
});

test('deployment spawn errors fail and clean up', async (t) => {
    const actual = await simulate(t, async ({ child }) => {
        child.emit('error', new Error('Cannot spawn'));
    });
    assert.equal(actual.code, 1);
    assert.equal(actual.killed, true);
    assert.match(actual.errors, /Cannot spawn/);
});

test('rejects invalid timeout before connecting', async () => {
    let errors = '';
    const code = await runTests({ host: '127.0.0.1', timeoutMs: NaN, stderr: { write(text) { errors += text; } } });
    assert.equal(code, 1);
    assert.match(errors, /positive number/);
});

test('missing host fails before deployment', async () => {
    let errors = '';
    const code = await runTests({ host: '', stderr: { write(text) { errors += text; } } });
    assert.equal(code, 1);
    assert.match(errors, /ROKU_HOST/);
});
