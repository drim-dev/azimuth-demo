import type { Metadata } from 'next';
import './globals.css';
import { DisplayDensityControl } from './display-density-control';

export const metadata: Metadata = {
  title: 'Ride — rider',
  description: 'The rider-facing site of the azimuth demo fixture.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>
        <header className="masthead">
          <div className="masthead__inner">
            <div className="masthead__identity">
              <p className="masthead__title">Ride</p>
              <span className="masthead__tag">rider</span>
            </div>
            <DisplayDensityControl />
          </div>
        </header>
        <main>{children}</main>
      </body>
    </html>
  );
}
