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
        className="h-16 w-16 text-slate-600"
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
        <p className="text-base font-medium text-slate-200">{t('viewer.noDataTitle')}</p>
        <p className="max-w-md text-sm text-slate-400">{t('viewer.noDataBody')}</p>
        <p className="font-mono text-xs break-all text-slate-500">{studyInstanceUid}</p>
      </div>

      <p className="max-w-md text-sm text-slate-500">
        {t('viewer.noDataHint', { command: 'pnpm seed:orthanc' })}
      </p>
    </div>
  );
}
