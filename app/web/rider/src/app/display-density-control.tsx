'use client';

import { useEffect, useState } from 'react';

type DisplayDensity = 'comfortable' | 'compact';

const storageKey = 'azimuth-demo:rider-display-density';

export function DisplayDensityControl() {
  const [density, setDensity] = useState<DisplayDensity>('comfortable');

  useEffect(() => {
    const stored = window.localStorage.getItem(storageKey);
    const selected = stored === 'compact' ? 'compact' : 'comfortable';
    setDensity(selected);
    document.documentElement.dataset.density = selected;
  }, []);

  function select(next: DisplayDensity) {
    setDensity(next);
    document.documentElement.dataset.density = next;
    window.localStorage.setItem(storageKey, next);
  }

  return (
    <fieldset className="density-control">
      <legend>Display density</legend>
      <button
        type="button"
        className="secondary"
        aria-pressed={density === 'comfortable'}
        onClick={() => select('comfortable')}
      >
        Comfortable
      </button>
      <button
        type="button"
        className="secondary"
        aria-pressed={density === 'compact'}
        onClick={() => select('compact')}
      >
        Compact
      </button>
    </fieldset>
  );
}
