# ZeroDrive - Open source file sync

ZeroDrive is an open source file synchronization service using `.NET`. Communication between services is handled via `gRPC`.

## Server
The server manages storage of the data, like a central repository. 
It supports different implementations of storage via the IStorageProvider interface.
Current implementations:
* SQLiteStorageProvider
* FileStorageProvider


### Configuration
Configuration is managed in `Server\appsettings.json`

## Client
Client requests the information about the repository, and manages updates and downloads to and from the repository.

### Configuration
Configuration is managed in `Client\appsettings.json`

## Type descriptions

* **Workspace:** A workspace is a tracked directory. This allows you to have multiple, independent "folders" tracked and independently placed on the clients operating system. A workspace is simply defined as a manifest of files and their structure in the tracked folder.
* **ZFile:** File information about a specific, tracked, file. it contains information about which workspace the file belongs to, it's path relative to the workspace, the full size, and hash (of chunk hashes), and the chunks related to this file.
* **Chunk:** A hash identifier for a set of data. This allows for simple data de-duplication. Each file is a list of chunk hashes, meaning no file contains a list of data itself, but instead references to chunks.
