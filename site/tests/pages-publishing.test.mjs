import test from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';

// Guard the deployment mistake where a second workflow publishes raw repository
// files over the tested Vite site. No YAML/runtime dependency is added.
const directory = new URL('../../.github/workflows/', import.meta.url);
const workflows = readdirSync(directory)
  .filter(name => /\.ya?ml$/.test(name))
  .map(name => ({ name, source: readFileSync(new URL(name, directory), 'utf8') }));
const canonical = workflows.find(file => file.name === 'compatibility-pages.yml');

test('only the verified compatibility workflow may publish GitHub Pages', () => {
  const publishers = workflows.filter(file => /uses:\s*actions\/deploy-pages@/.test(file.source));
  assert.deepEqual(publishers.map(file => file.name), ['compatibility-pages.yml'],
    'a competing publisher can overwrite the built site with repository files');
});

test('Pages uploads only site/dist after installation, model, build and browser checks', () => {
  assert.ok(canonical, 'canonical site workflow is present');
  const source = canonical.source;
  const upload = source.indexOf('uses: actions/upload-pages-artifact@');
  assert.ok(upload > 0);
  let previous = -1;
  for (const command of ['run: npm ci', 'run: npm test', 'run: npm run build', 'run: npm run test:browser']) {
    const index = source.indexOf(command);
    assert.ok(index > previous && index < upload, `${command} must run before Pages upload`);
    previous = index;
  }
  assert.match(source.slice(upload), /with:\s*\n\s+path: site\/dist\s*\n/);
  assert.match(source, /needs: verify-build/);
  assert.equal(source.split("if: github.ref == 'refs/heads/main' && github.event_name != 'pull_request'").length - 1, 2,
    'both Pages upload and deploy must remain main-only and exclude pull requests');
});

test('workflow changes run the publication regression on PR and main', () => {
  assert.equal(canonical.source.split("'.github/workflows/**'").length - 1, 2,
    'a new publisher must be checked before it reaches main');
});

test('public deployment is checked against the tested HTML and data bytes', () => {
  const source = canonical.source;
  assert.match(source, /outputs:\n\s+index-sha256: \$\{\{ steps\.site-identity\.outputs\.index \}\}/);
  assert.match(source, /data-sha256: \$\{\{ steps\.site-identity\.outputs\.data \}\}/);
  const deploy = source.indexOf('uses: actions/deploy-pages@');
  const smoke = source.indexOf('- name: Verify published entrypoint, data and assets');
  assert.ok(smoke > deploy, 'the public check runs only after actual Pages deployment');
  assert.match(source.slice(smoke), /EXPECTED_INDEX: \$\{\{ needs\.verify-build\.outputs\.index-sha256 \}\}/);
  assert.match(source.slice(smoke), /EXPECTED_DATA: \$\{\{ needs\.verify-build\.outputs\.data-sha256 \}\}/);
});
