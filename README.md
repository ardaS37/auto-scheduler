# AutoScheduler

A Windows desktop application for creating and managing school timetables.

Windows üzerinde okul ders programlarını oluşturmak ve yönetmek için masaüstü uygulaması.

## Features / Özellikler

- Create teachers, classes, courses, rooms, and time slots.
- Generate schedules while considering availability and assignment constraints.
- Review timetable health, edit assignments, and export schedules.
- Save projects locally and reopen recent work.

## Build / Derleme

Requires Windows and the .NET 10 SDK.

```powershell
dotnet build .\AutoScheduler.csproj
dotnet publish .\AutoScheduler.csproj -c Release -r win-x64 --self-contained true
```

## Release / Yayın

The GitHub Releases page provides a self-contained 64-bit Windows build.

GitHub Releases sayfasında bağımsız çalışabilen 64-bit Windows sürümü bulunur.

## Privacy / Gizlilik

The application stores timetable project files locally. Do not commit real student, teacher, or school data to public repositories.

Uygulama ders programı proje dosyalarını yerel olarak saklar. Gerçek öğrenci, öğretmen veya okul verilerini herkese açık depolara yüklemeyin.

## License

No license is granted. All rights reserved.
