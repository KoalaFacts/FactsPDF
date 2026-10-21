"""Run fresh package consumption with explicit exact stable SDK/runtime constraints.

This prepares release evidence; it does not publish or designate a version as GA.
"""
from verify_first_use import main


if __name__ == '__main__':
    main(strict_toolchain=True)
