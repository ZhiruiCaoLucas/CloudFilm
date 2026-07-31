'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import type { Study } from '@cloudfilm/shared';

/**
 * Keyboard study switching, using the bracket keys a PACS workstation
 * conventionally binds to previous and next.
 *
 * Reading is a two-handed, high-volume activity; reaching for the study list
 * with a pointer for every comparison is the kind of friction that makes
 * software feel unprofessional.
 *
 * Two deliberate constraints. Typing into a field never switches study, so the
 * handler ignores events from inputs and editable content. And any modifier
 * combination is left alone, so browser and operating system shortcuts keep
 * working.
 *
 * Note that while focus is inside the viewer iframe the parent document does
 * not receive key events at all. That is expected: those keystrokes belong to
 * the viewer's own tools.
 */
export function useStudyShortcuts(studies: readonly Study[], selectedStudyInstanceUid: string) {
  const router = useRouter();

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) {
        return;
      }

      if (event.key !== '[' && event.key !== ']') {
        return;
      }

      const target = event.target;
      if (
        target instanceof HTMLElement &&
        (target.isContentEditable ||
          target instanceof HTMLInputElement ||
          target instanceof HTMLTextAreaElement ||
          target instanceof HTMLSelectElement)
      ) {
        return;
      }

      const current = studies.findIndex(
        (study) => study.studyInstanceUid === selectedStudyInstanceUid,
      );
      if (current === -1) {
        return;
      }

      const next = event.key === '[' ? current - 1 : current + 1;
      const nextStudy = studies[next];
      if (nextStudy === undefined) {
        return;
      }

      event.preventDefault();
      router.push(`/workspace/${encodeURIComponent(nextStudy.studyInstanceUid)}`);
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [router, studies, selectedStudyInstanceUid]);
}
