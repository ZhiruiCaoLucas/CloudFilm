'use client';

import { useTranslations } from '@/components/locale-provider';

/**
 * Shown when the open study is a placeholder rather than imported DICOM.
 *
 * Takes precedence over the viewer's own availability: even with OHIF running,
 * handing it a synthetic UID produces an empty viewer with no explanation. This
 * states the actual reason instead.
 */
export function ViewerNoData({ studyInstanceUid }: { readonly studyInstanceUid: string }) {
  const t = useTranslations();

  return (
    <div className="flex h-full flex-col items-center justify-center gap-5 px-6 text-center">
      <svg
        viewBox="0 0 48 48"
        className="h-16 w-16 text-[#2b3a55]"
        role="presentation"
        aria-hidden="true"
        focusable="false"
      >
        <rect
          x="8"
          y="6"
          width="32"
          height="36"
          rx="3"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
        />
        <path
          d="M16 16h16M16 24h16M16 32h10"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
        />
      </svg>

      <div className="flex flex-col gap-2">
        <p className="text-[15px] font-semibold tracking-[-0.015em] text-[#dbe6fb]">
          {t('viewer.noDataTitle')}
        </p>
        <p className="atrium-body max-w-md text-[#93a6c6]">{t('viewer.noDataBody')}</p>
        <p className="font-mono text-[11.5px] break-all text-[#6c7f9f]">{studyInstanceUid}</p>
      </div>

      <p className="max-w-md text-[12.5px] leading-relaxed text-[#6c7f9f]">
        {t('viewer.noDataHint', { command: 'pnpm seed:orthanc' })}
      </p>
    </div>
  );
}
