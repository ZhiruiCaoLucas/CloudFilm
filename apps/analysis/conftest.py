"""Makes `app` importable however pytest is invoked.

The service is a plain directory rather than an installed package - it is
deployed as a container, not published to an index - so the tests need its root
on the path whether they are run from here or from the repository root.
"""

import sys
from pathlib import Path

_ROOT = str(Path(__file__).parent)

if _ROOT not in sys.path:
    sys.path.insert(0, _ROOT)
