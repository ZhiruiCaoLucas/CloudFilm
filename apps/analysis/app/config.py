"""Settings the analysis service reads from its environment.

Deliberately tiny. This service holds no credentials, talks to no database and
calls nothing, so there is almost nothing to configure - and every setting that
does not exist is one that cannot be set wrongly in a hospital deployment.
"""

from __future__ import annotations

import os

#: How much DICOM one request may carry, in bytes.
#:
#: Conservative on purpose. This is a first implementation that reads pixel data
#: into memory, and a real study can be far larger than this; the limit exists so
#: that the failure is an immediate, obvious 413 rather than a container the
#: kernel kills halfway through. Raising it is a deliberate act, and one that
#: should be taken together with the streaming caveat in the README.
DEFAULT_MAX_UPLOAD_BYTES = 64 * 1024 * 1024

#: How much of one upload is read at a time.
#:
#: Small enough that the size limit is enforced long before the whole body has
#: arrived, large enough not to make a syscall per kilobyte.
READ_CHUNK_BYTES = 1024 * 1024


def max_upload_bytes() -> int:
    """Return the configured maximum upload size in bytes.

    Read on every call rather than captured at import, so a test can set the
    variable without reloading the module and an operator can see the value they
    set take effect on a restart rather than on a rebuild.

    An unset, empty, unparseable or non-positive value falls back to the default:
    a size limit that silently became "no limit" would be the worst outcome, and
    refusing to start over a typo in a tuning knob would be the second worst.
    """
    raw = os.environ.get("ANALYSIS_MAX_UPLOAD_BYTES", "").strip()

    if not raw:
        return DEFAULT_MAX_UPLOAD_BYTES

    try:
        configured = int(raw)
    except ValueError:
        return DEFAULT_MAX_UPLOAD_BYTES

    return configured if configured > 0 else DEFAULT_MAX_UPLOAD_BYTES
