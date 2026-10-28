# ZeroDrive TODOs

## 1. DevOps & TDD Foundation
- [ ] Automated CI/CD pipeline (build and test execution)
- [x] `Core.Tests` TDD suite (`IChunker`, `IContentHasher`, and metadata models)
- [ ] `Server.Tests` TDD suite (`IStorageProvider`)
- [ ] `Client.Tests` TDD suite (local directory watching, `SyncWorker` logic)

## 2. Core Library (`ZeroDrive.Core`)
- [ ] File chunking and reassembly interfaces and simple fixed size chunking (`IChunker`, `FixedSizeChunker`)
- [x] Content and whole-file hashing interface and SHA256 hasher (`IContentHasher`, `SHA256ContentHasher`)
- [ ] Domain-to-Protobuf mapping extensions (`ZFileInfo`, `ZWorkspaceData`)
- [ ] Protobuf definitions (`sync.proto`)

## 3. Baseline Server (`ZeroDrive.Server`)
- [ ] Storage provider abstraction and DI configuration (`IStorageProvider`, `StorageProviderBase`)
- [ ] SQLite and RepoDb storage implementation (`SQLiteStorageProvider`)
- [x] Local filesystem storage implementation (`FileStorageProvider`)
- [ ] gRPC service endpoints (`SyncService`)
- [ ] Server-side file integrity verification(?)
- [ ] Two-phase file upload (Stage + Commit)
- [ ] Concurrent modification handling
- [ ] Delta file re-downloading
- [ ] File deletion propagation

## 4. Baseline Client (`ZeroDrive.Client`)
- [ ] Local workspace indexing and state persistence (folder reconcilliation on startup)
- [ ] Filesystem event monitoring and debouncing (`DirectoryWatcher`)
- [ ] Local-to-remote manifest diffing (`SyncWorker`)
- [ ] Missing chunk upload pipeline
- [ ] Chunk download and local file reconstruction
- [ ] Sync loop suppression (preventing `DirectoryWatcher` from triggering uploads on newly downloaded files)
- [ ] File deletion propagation

## 5. Lifecycle & Storage Management
- [ ] Workspace deletion
- [ ] Orphaned chunk garbage collection

## 7. Real-Time Sync & Future Features
- [ ] gRPC manifest update feed
- [ ] HTTP/2 keep-alive heartbeat
- [ ] Client auto-reconnection and state reconciliation
- [ ] Dynamic configuration reloading
- [ ] Authentication
- [ ] Automated deployment / release artifacts in CI/CD