# Changelog

## v1.2.0 - 2020-01-06

### Added

- New command line option `--process-restarts` that informs the injector the process restarts itself
- New command line option `--wait-for-dlls`. Used to let the injector know to monitor for the specified dlls before attempting to inject
- Injector will try its best to detect when a process has initialized by monitoring DLLs loaded into the process

### Changes

- Injector will detect invalid DLL paths sooner

## v1.1.0 - 2019-12-27

### Changes

- Auto detects Microsoft store apps, so command line/config option is not explicitly needed

## v1.0.0 - 2019-12-11

- Initial release