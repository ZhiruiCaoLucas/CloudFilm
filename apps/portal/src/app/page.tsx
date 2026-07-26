import { PatientSearchForm } from '@/components/patient-search-form';
import { RecentSearches } from '@/components/recent-searches';

export const metadata = {
  title: 'Patient search · CloudFilm',
};

export default function HomePage() {
  return (
    <div className="flex flex-col gap-16">
      <section className="flex flex-col items-center gap-8 pt-8 text-center sm:pt-16">
        <div className="flex flex-col gap-3">
          <h1 className="text-foreground text-3xl font-semibold tracking-tight sm:text-4xl">
            Cloud Imaging Platform
          </h1>
          <p className="text-muted text-base">Secure access to medical imaging studies.</p>
        </div>

        <div className="w-full max-w-xl text-left">
          <PatientSearchForm />
        </div>
      </section>

      <RecentSearches />
    </div>
  );
}
