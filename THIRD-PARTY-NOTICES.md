# Third-party notices

## aeronautic

The flare model (`Source/FlareModel.cs`, `Source/Graybody.cs`) is ported from [aeronautic](https://github.com/Romainlg29/aeronautic) by Romain Le Gall, `@aeronautic/countermeasures` (`flare.ts`, `graybody.ts`, `trail.ts`) at commit `db04669`. Each ported file names its upstream source. That code is used under the MIT License:

```
MIT License

Copyright (c) 2026 Romain Le Gall

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The upstream model rests on Debnath et al., CEJEM 22(2), 2025 (MTV1 burn rate and band energy) and the CIE 1924 photopic luminosity function.

## BDArmory

This mod does not contain or redistribute any BDArmory code, textures or models. It finds BDArmory's flares at run time through reflection and changes how they are drawn. BDArmory is distributed under CC BY-SA 2.0 by its own authors.
