import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { CrossOutline, DotField, EcgHairline } from '@cloudfilm/ui';
import { PatientSearchForm } from '@/components/patient-search-form';
import { RecentSearches } from '@/components/recent-searches';
import { PortalShell } from '@/components/portal-shell';
import { currentUser } from '@/lib/api';
import { serverTranslations } from '@/lib/locale';

export async function generateMetadata(): Promise<Metadata> {
  const t = await serverTranslations();

  return { title: t('home.pageTitle') };
}

/**
 * Patient lookup.
 *
 * Four bands, top to bottom, on Atrium's four surface steps: the page ground,
 * a pale blue hero carrying a photograph of the hospital, the white console
 * raised off it, and the panels below.
 */
export default async function HomePage() {
  const [user, t] = await Promise.all([currentUser(), serverTranslations()]);

  return (
    <PortalShell user={user} wide>
      {/* ------------------------------------------------------------------ */}
      {/* Hero band: copy on the left, the building on the right.             */}
      {/* ------------------------------------------------------------------ */}
      <section className="border-atrium-border bg-atrium-sky-100 atrium-enter-1 relative overflow-hidden rounded-2xl border px-8 pt-[56px] pb-[136px] sm:px-14">
        {/*
          The photograph. Dropped entirely below `md`, where there is no room
          for it beside the copy and it would only push the search below the
          fold. Masked at every edge, so the band never shows a photographic
          corner.
        */}
        <div
          aria-hidden="true"
          className="atrium-hero-photo pointer-events-none absolute inset-y-0 right-0 hidden w-[54%] md:block"
        >
          <Image
            src="/images/hospital-exterior.jpg"
            alt=""
            fill
            sizes="(min-width: 1180px) 640px, 55vw"
            priority
            className="object-cover object-[62%_38%]"
          />
        </div>

        {/* The blue that holds it inside Atrium, then the ground the copy
            sits on. Both inert. */}
        <div
          aria-hidden="true"
          className="atrium-hero-tint pointer-events-none absolute inset-0 hidden md:block"
        />

        <DotField className="left-auto w-[46%] opacity-60" />

        {/*
          The cross is anchored to the seam between ground and photograph rather
          than to an empty corner: roughly half of it falls on the building, and
          the band crops two of its four arms. It reads as an object in the
          composition instead of a mark floating in a field.
        */}
        <CrossOutline
          size={320}
          className="absolute -top-14 right-[26%] hidden text-white/45 md:block"
        />
        <CrossOutline size={280} className="absolute -top-12 -right-16 opacity-50 md:hidden" />

        <div className="relative max-w-[31rem]">
          <span className="atrium-eyebrow text-atrium-primary">{t('home.heroEyebrow')}</span>
          <h1 className="atrium-display text-atrium-ink mt-[18px] text-[clamp(2rem,4vw,50px)]">
            {t('home.title')}
          </h1>
          <EcgHairline width={260} height={22} className="mt-[22px] -ml-1" />
          <p className="atrium-body text-atrium-body mt-[18px] max-w-[29rem]">
            {t('home.subtitle')}
          </p>
        </div>
      </section>

      {/* ------------------------------------------------------------------ */}
      {/* The console. The page's one action, raised off the band above it.   */}
      {/* ------------------------------------------------------------------ */}
      <section
        aria-labelledby="patient-search-heading"
        className="atrium-console atrium-enter-2 relative z-10 mx-auto -mt-[92px] w-[calc(100%-2rem)] px-6 pt-7 pb-6 sm:w-[calc(100%-5rem)] sm:px-10 sm:pt-9 sm:pb-7"
      >
        <h2 id="patient-search-heading" className="atrium-eyebrow text-atrium-muted">
          {t('home.searchLabel')}
        </h2>

        <div className="mt-[18px]">
          <PatientSearchForm />
        </div>

        {/* Full-bleed rule: the divider belongs to the card, so it runs the
            width of the card rather than the width of the content inside it. */}
        <div className="border-atrium-border -mx-6 mt-7 border-t pt-5 sm:-mx-10 sm:mt-8 sm:pt-6">
          <div className="px-6 sm:px-10">
            <Link
              href="/search"
              className="atrium-next text-atrium-primary hover:text-atrium-deep focus-visible:outline-atrium-primary inline-flex items-center gap-2 rounded text-[13.5px] font-semibold focus-visible:outline-2 focus-visible:outline-offset-4"
            >
              {t('home.advancedSearch')}
              <span aria-hidden="true" className="text-[15px] leading-none">
                →
              </span>
            </Link>
          </div>
        </div>
      </section>

      {/* ------------------------------------------------------------------ */}
      {/* Recent work, and the rule the product follows with it.              */}
      {/* ------------------------------------------------------------------ */}
      <div className="atrium-enter-3 mt-[56px] grid items-start gap-[34px] lg:grid-cols-[minmax(0,1fr)_300px]">
        <RecentSearches />

        {/*
          Supporting context, not a second feature. No card and no border box -
          a rule down the leading edge, an eyebrow, the explanation, then the two
          facts it rests on. It is narrower than the list beside it and quieter
          than it on purpose.
        */}
        <section
          aria-labelledby="privacy-note-heading"
          className="border-atrium-border lg:mt-[30px] lg:border-l lg:pl-7"
        >
          <h2 id="privacy-note-heading" className="atrium-eyebrow text-atrium-muted">
            {t('home.privacyTitle')}
          </h2>
          <p className="text-atrium-body mt-3 text-[13px] leading-[1.65]">
            {t('home.privacyBody')}
          </p>

          <dl className="border-atrium-border mt-5 border-t">
            {[
              { term: t('home.recentTitle'), detail: t('home.privacyPointStorage') },
              { term: t('study.open'), detail: t('home.privacyPointAudit') },
            ].map((point) => (
              <div key={point.term} className="border-atrium-border border-b py-3">
                <dt className="atrium-eyebrow text-atrium-muted text-[10px]">{point.term}</dt>
                <dd className="text-atrium-ink mt-1.5 text-[12.5px] leading-snug font-medium">
                  {point.detail}
                </dd>
              </div>
            ))}
          </dl>
        </section>
      </div>
    </PortalShell>
  );
}
