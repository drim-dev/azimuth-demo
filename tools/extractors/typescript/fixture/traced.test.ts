import { covers, untraced } from '@azimuth/annotations';

declare function test(name: string, body: () => void): void;

test('the route answers', () => {
  covers('alpha', 'route-thing', 'component', 'invariant');
});

test('the projection redacts', () => {
  covers('alpha', 'projection-thing', 'e2e', 'example', 'model-based');
});

test('the harness boots', () => {
  untraced('smoke check; maps to no claim by design');
});

test('a bare test declaring nothing', () => {
  const x = 1;
  void x;
});
