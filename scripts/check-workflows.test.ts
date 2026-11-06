import assert from 'node:assert/strict';
import { test } from 'node:test';
import { checkWorkflow } from './check-workflows.ts';

const prOnly = 'name: X\non:\n  pull_request:\npermissions:\n  contents: read\njobs:\n  a:\n    runs-on: ubuntu-latest\n';

test('a pull_request workflow without a concurrency group is reported', () => {
  assert.deepEqual(checkWorkflow('x.yml', prOnly), [
    'x.yml: pull_request workflow has no top-level concurrency group keyed on github.ref',
  ]);
});

test('a group keyed on github.ref must also decide cancel-in-progress', () => {
  const text = prOnly.replace('jobs:', 'concurrency:\n  group: x-${{ github.ref }}\njobs:');
  assert.deepEqual(checkWorkflow('x.yml', text), ['x.yml: concurrency group does not set cancel-in-progress']);
});

test('a group not keyed on github.ref is reported', () => {
  const text = prOnly.replace('jobs:', 'concurrency:\n  group: x\n  cancel-in-progress: true\njobs:');
  assert.deepEqual(checkWorkflow('x.yml', text), [
    'x.yml: pull_request workflow has no top-level concurrency group keyed on github.ref',
  ]);
});

test('a keyed group with cancel-in-progress passes, including the expression and the inline trigger list', () => {
  const keyed = prOnly.replace(
    'jobs:',
    "concurrency:\n  group: x-${{ github.ref }}\n  cancel-in-progress: ${{ github.event_name == 'pull_request' }}\njobs:",
  );
  assert.deepEqual(checkWorkflow('x.yml', keyed), []);
  assert.deepEqual(
    checkWorkflow('y.yml', 'on: [push, pull_request]\nconcurrency:\n  group: y-${{ github.ref }}\n  cancel-in-progress: true\n'),
    [],
  );
  const expression =
    "on:\n  pull_request:\nconcurrency:\n  group: z-${{ github.event_name == 'workflow_dispatch' && 'manual' || github.ref }}\n  cancel-in-progress: false\n";
  assert.deepEqual(checkWorkflow('z.yml', expression), []);
  assert.deepEqual(
    checkWorkflow('r.yml', 'on:\n  pull_request:\nconcurrency:\n  group: r-${{ github.ref_name }}\n  cancel-in-progress: true\n'),
    ['r.yml: pull_request workflow has no top-level concurrency group keyed on github.ref'],
  );
});

test('workflows without a pull_request trigger, and nested keys named pull_request, are ignored', () => {
  assert.deepEqual(checkWorkflow('m.yml', 'on:\n  push:\n    branches: [main]\njobs:\n  a:\n    if: 1\n'), []);
  assert.deepEqual(
    checkWorkflow('n.yml', 'on:\n  workflow_dispatch:\njobs:\n  pull_request:\n    runs-on: ubuntu-latest\n'),
    [],
  );
});
