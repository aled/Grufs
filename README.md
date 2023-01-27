# Wibblr.Grufs

A deduplicating, encrypted file storage system. Works in two modes:

- as a repository for backups
- as a repository for continuous syncing of files from multiple clients

## Encryption Goals
- Use standard algorithms only.
- Data must be deduplicated - the same plaintext must be deduplicated despite being encrypted.
- Everything in the repository must be readable using a single password
- Accept that there might be exploitable security flaws in the design. Don't store your bank password using this.
- Initially, simply prevent automated scanning by cloud storage providers.
- Eventually, use of reviews/audits and appropriate fixes, to improve security over time to that of similar products (borg, restic, cryptomator etc.)
- Ability to prevent clients deleting/overwriting data. This may require an active server process (e.g. HTTP) to achieve. Use of SFTP may require additional
  snapshots (e.g. using ZFS) on the server.

## Threat Model
- Automated scans and breaches of cloud storage providers
- Local storage is considered secure; i.e. use bitlocker or similar if local encryption is required.

## Security Architecture
### Addressing
Chunks may be content, indexes, or key-value pairs.

### Content-defined addressing
- Each content chunk is assigned an address, which is a hash of the plaintext content using HMAC-SHA-256 with a master content addressing key. This key is encrypted and stored 
  in the repository metadata alongside the master repository key. Deduplication relies on identical content resolving to identical addresses, so content chunks contain no
  other data apart from the raw content.

### Key-value pair addressing
- To store key-value pairs, the address is calculated using a hash of the key using HMAC-SHA-256 with a master dictionary addressing key. Before hashing, a namespace is prepended
  to the key. 
- Key-value pairs may be also be stored using a Versioned variant. This uses a different master addressing key, and additionally prepends a sequence number to the key before hashing.

### Streams
- Streams are converted into chunks by use of indexes. An index is a stream containing a list of addresses. Very long files may have several 'levels' of index.

  e.g. a stream with 9 chunks:
  | content | address  |
  | ------- | -------  |
  | 'the'   | 3C456DE6 |
  | 'quick' | 8DC71ED7 |
  | 'brown' | 8DC71ED7 |
  | 'fox'   | 0FA0E0E6 |
  | 'jumps' | EF7DC95C |
  | 'over'  | B4FCA359 |
  | 'the'   | 3C456DE6 |
  | 'lazy'  | E5A86E54 |
  | 'dog'   | 812C397D |


  has index level 1 with 3 chunks:
  | content                      | address  |
  | -------                      | -------  |
  | '3C456DE6,8DC71ED7,8DC71ED7' | 60025A86 |
  | '0FA0E0E6,EF7DC95C,B4FCA359' | 47BE1904 |
  | '3C456DE6,E5A86E54,812C397D' | 787C148B |

  and index level 2 with one chunk:  
  | content                      | address  |
  | -------                      | -------  |
  | '60025A86,47BE1904,787C148B' | D41ADA6E |
 
   A stream is identified by the address of the highest index and the index level, so in this case (D41ADA6E, 2)

### Encryption
- Files are split into chunks. Each chunk is encrypted with AES-256 in CBC mode, with a cryptographically secure random key and initialization vector.
- A checksum is taken of the plaintext, and this is appended to the plaintext before encryption. This checksum is validated on decryption.
- The chunk's encryption key is wrapped using the repository master key. This uses the RFC3394 key wrapping algorithm.
- The IV, wrapped encryption key, and a plaintext checksum are stored alongside the encrypted data. This checksum is also validated on decryption, although
  its main purpose is to allow a trusted server to validate the integrity of stored data.
- The repository master key is encrypted and stored inside the repository metadata, using an encryption key derived from the repository password using the RFC2989
  key derivation algorithm with a random salt and 500000 iterations.
- The repository metadata itself (containing the encrypted master keys) is stored in the repository effectively in plaintext, as a key-value pair with a well-known
  encryption key and salt. (It would be possible to use a second password to have a random address and encrypt the entire metadata, but this is not necessary for security)