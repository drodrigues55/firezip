# Third-party notices

## SharpSevenZip

Firezip uses SharpSevenZip 2.0.128 to create ZIP and 7z archives. SharpSevenZip is licensed under the GNU Lesser General Public License, version 3 or later (LGPL-3.0-or-later).

- Source: [SharpSevenZip](https://github.com/JeremyAnsel/SharpSevenZip)
- License: [LGPL-3.0-or-later](https://www.gnu.org/licenses/lgpl-3.0.html)

## 7-Zip native library

The SharpSevenZip NuGet package includes x86 and x64 native `7z.dll` files. The packaged DLLs report version 26.03. 7-Zip's license information identifies the core code as LGPL-2.1-or-later, with some BSD-licensed files and an additional unRAR restriction for the RAR decoder.

- Source and license details: [7-Zip license information](https://github.com/ip7z/7zip/blob/main/DOC/License.txt)
- 7-Zip source and downloads: [7-zip.org](https://www.7-zip.org/)
- Firezip uses the DLL to create ZIP/7z and decode supported archives; Firezip does not create RAR archives.

The native DLLs remain separate from Firezip's managed code so they can be replaced with compatible builds. Public redistribution must include the applicable license notices and source information for both SharpSevenZip and 7-Zip.

## SharpCompress

Firezip uses SharpCompress 0.50.4 to read and extract archives and to create formats outside ZIP and 7z. It is licensed under the MIT License.

- Source: [SharpCompress](https://github.com/adamhathcock/sharpcompress)
- License: [MIT](https://opensource.org/license/mit/)
