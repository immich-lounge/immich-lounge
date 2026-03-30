const fs = require('fs');
const path = require('path');
const http = require('http');
const { spawnSync } = require('child_process');
const rokuDeploy = require('roku-deploy');

const variants = {
    stick: {
        buildProject: 'bsconfig.json',
        configFile: 'roku-deploy.json',
        launchHostEnv: 'ROKU_HOST',
        packageBaseName: 'immich-lounge-stick'
    },
    screensaver: {
        buildProject: 'bsconfig.screensaver.json',
        configFile: 'roku-deploy.screensaver.json',
        packageBaseName: 'immich-lounge-screensaver'
    },
    test: {
        buildProject: 'bsconfig.test.json',
        configFile: 'roku-deploy.test.json',
        packageBaseName: 'immich-lounge-test'
    }
};

async function main() {
    const variantName = process.argv[2] || 'stick';
    const variant = variants[variantName];
    if (!variant) {
        throw new Error(`Unknown deploy variant "${variantName}"`);
    }

    const runId = `${variantName}-${Date.now()}`;
    const runRoot = path.resolve('_out', 'deploy', runId);
    const buildDir = path.join(runRoot, 'app');
    const deployStagingDir = path.join(runRoot, 'upload-staging');
    const outDir = path.join(runRoot, 'pkg');

    runBsc(variant.buildProject, buildDir);

    const config = loadConfig(variant.configFile);
    disableProxyForHost(config.host);
    const options = {
        ...config,
        rootDir: buildDir,
        stagingDir: deployStagingDir,
        outDir,
        outFile: variant.packageBaseName,
        files: ['**/*'],
        retainStagingDir: true,
        retainStagingFolder: true,
        retainDeploymentArchive: true
    };

    await rokuDeploy.prepublishToStaging(options);
    await rokuDeploy.zipPackage(options);
    const publishResult = await rokuDeploy.publish(options);
    console.log(`Publish: ${publishResult.message}`);

    if (variant.launchHostEnv) {
        const host = process.env[variant.launchHostEnv];
        if (!host) {
            throw new Error(`Missing environment variable ${variant.launchHostEnv}`);
        }
        await launchDevChannel(host);
    }
}

function runBsc(projectFile, stagingDir) {
    const command = `npx bsc --project "${projectFile}" --stagingDir "${stagingDir}" --createPackage false`;
    const result = spawnSync(command, [], { stdio: 'inherit', shell: true });

    if (result.status !== 0) {
        throw new Error(`Build failed for ${projectFile}`);
    }
}

function loadConfig(configFile) {
    const configPath = path.resolve(configFile);
    const raw = fs.readFileSync(configPath, 'utf8');
    return expandEnv(JSON.parse(raw));
}

function expandEnv(value) {
    if (typeof value === 'string') {
        return value.replace(/\$\{([^}]+)\}/g, (_, name) => process.env[name] || '');
    }
    if (Array.isArray(value)) {
        return value.map(expandEnv);
    }
    if (value && typeof value === 'object') {
        const result = {};
        for (const [key, child] of Object.entries(value)) {
            result[key] = expandEnv(child);
        }
        return result;
    }
    return value;
}

function disableProxyForHost(host) {
    if (!host) {
        return;
    }

    delete process.env.HTTP_PROXY;
    delete process.env.HTTPS_PROXY;
    delete process.env.http_proxy;
    delete process.env.https_proxy;

    const existing = process.env.NO_PROXY || process.env.no_proxy || '';
    const entries = existing
        .split(',')
        .map((x) => x.trim())
        .filter(Boolean);

    if (!entries.includes(host)) {
        entries.push(host);
    }

    const noProxy = entries.join(',');
    process.env.NO_PROXY = noProxy;
    process.env.no_proxy = noProxy;
}

function launchDevChannel(host) {
    return new Promise((resolve, reject) => {
        const req = http.request(
            {
                host,
                port: 8060,
                path: '/launch/dev',
                method: 'POST'
            },
            (res) => {
                console.log(`Launched: ${res.statusCode}`);
                if (res.statusCode >= 200 && res.statusCode < 300) {
                    resolve();
                } else {
                    reject(new Error(`Launch failed with status ${res.statusCode}`));
                }
            }
        );
        req.on('error', reject);
        req.end();
    });
}

main().catch((error) => {
    console.error(error);
    process.exit(1);
});
