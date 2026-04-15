# PROJECT_STATE

## Project
Image Studio

Desktop application for image generation with a Python backend and a C# WPF client.

---

## Vision

The goal is to build a local-first image generation studio with:

- Python backend for inference, analysis, model management, and job orchestration
- WPF desktop client for user workflows, previews, history, presets, runtime diagnostics, and future theme support
- support for text-to-image and analyze+generate workflows
- stable job queue, progress tracking, and cancel support
- future polished Windows 11 style UI with light/dark/auto theme switching

---

## Current Architecture

## Backend
Tech stack:
- FastAPI
- PyTorch
- diffusers
- Stable Diffusion / SDXL pipelines
- local file-based storage
- in-memory job queue

Main backend responsibilities:
- model listing and installation
- image generation
- analyze + generate workflow
- image serving
- runtime info reporting
- job queue, progress, and cancellation

Key backend modules:
- `app/main.py`
- `app/api/routes_generation.py`
- `app/api/routes_analysis.py`
- `app/api/routes_images.py`
- `app/api/routes_health.py`
- `app/api/routes_runtime.py`
- `app/services/generation_service.py`
- `app/services/job_service.py`
- `app/services/image_caption_service.py`
- `app/services/caption_parser_service.py`
- `app/services/prompt_builder_service.py`

## Client
Tech stack:
- C# WPF
- MVVM-style structure
- partial `MainViewModel`
- local storage for history, presets, workspace state

Main client responsibilities:
- server selection and management
- model selection and install
- prompt editing
- analyze+generate workflow
- text generation workflow
- polling backend jobs
- cancel active jobs
- history browsing
- runtime info display
- image previews and export

Key client modules:
- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `ViewModels/MainViewModel*.cs`
- `Services/ApiClientService.cs`
- `Services/HistoryService.cs`
- `Services/WorkspaceStateService.cs`
- `Services/GenerationPresetService.cs`
- `Services/ThemeService.cs`
- `Models/GenerationJobStatus.cs`
- `Models/RuntimeInfo.cs`

---

## What Works

## Backend
Working:
- `/health`
- `/models`
- `/models/install`
- `/generate`
- `/generate-job`
- `/jobs/{job_id}`
- `/jobs/{job_id}/cancel`
- `/analyze-and-generate`
- `/analyze-and-generate-job`
- `/images/{filename}`
- `/runtime-info`

Generation system:
- shared generation service
- diffusers pipeline loading
- local model registry
- queue-based job processing
- single active generation job at a time
- progress reporting
- job cancellation
- analyze+generate background jobs
- runtime GPU diagnostics
- CUDA / CPU detection
- model pipeline cache

## Client
Working:
- server profile management
- model refresh and install
- image selection and drag & drop
- prompt override and negative prompt override
- text generation
- generate from final prompt with progress
- analyze+generate via job endpoint
- job polling
- cancel button in status bar
- runtime info shown in UI
- image download and preview
- history save/load
- generation presets
- workspace persistence
- partial split of `MainViewModel`

## Theme foundation
Working:
- theme resource dictionaries introduced
- light/dark/auto foundation prepared
- theme service introduced
- app startup theme application available

---

## In Progress

### Client UI / Theme
In progress:
- converting hardcoded colors to theme resources
- preparing proper light / dark / auto switching in settings
- Windows 11 style polish
- separating styles into dedicated theme files

### Client architecture
In progress:
- splitting `MainViewModel.cs` into partial files
- reducing single-file complexity
- preparing future extraction of workflow logic into services/coordinators

---

## Not Finished Yet

### Client
Planned:
- full settings UI for theme mode
- better Windows 11 visual style
- more complete dynamic resource usage
- cleaner button hierarchy and layout system
- runtime info panel polish
- possible queue position display
- optional cancel button placement improvements
- possible thumbnail caching
- more structured status messaging

### Backend
Planned:
- persistent jobs across restarts
- pipeline unload policy / cache strategy
- more advanced runtime/system info
- possible benchmark endpoint
- stronger error contracts
- optional queue info endpoint

### Documentation
Planned:
- dedicated architecture docs
- dedicated API contract doc
- roadmap doc
- developer setup notes

---

## Job System Summary

Current job design:
- jobs are stored in memory
- one worker processes one generation job at a time
- queued jobs wait in FIFO order
- running jobs report progress
- running and queued jobs can be cancelled
- job state is exposed through `/jobs/{job_id}`

Current statuses:
- `queued`
- `running`
- `completed`
- `failed`
- `cancelled`

Important limitation:
- jobs are not persisted across backend restart

---

## Runtime / GPU Summary

Current backend behavior:
- backend chooses `cuda` when `torch.cuda.is_available()` is true
- otherwise it falls back to `cpu`
- runtime diagnostics are exposed via `/runtime-info`
- client displays device, GPU name, VRAM, and cached models

Current target hardware:
- desktop RTX 3090
- mobile RTX 4070

Important note:
- if `/runtime-info` reports CPU mode, the issue is likely environment / PyTorch / CUDA setup, not app logic

---

## Current Recommended Next Steps

### Priority 1
Finish client theme system:
- move more hardcoded colors into theme resources
- add user-selectable Light / Dark / Auto mode
- store theme preference
- make UI visually closer to Windows 11

### Priority 2
Continue client cleanup:
- finish splitting `MainViewModel`
- reduce duplication in generation/analyze workflows
- consider moving workflow orchestration to dedicated services

### Priority 3
Improve documentation:
- backend README
- client README
- architecture notes
- roadmap

### Priority 4
Backend polish:
- improve pipeline cache policy
- possibly add queue info endpoint
- possibly add persistence for jobs

---

## Known Risks / Technical Debt

- `MainViewModel` logic is cleaner than before but still large
- backend jobs are in-memory only
- pipeline cache can grow if many models are loaded
- WPF styling is still partly hardcoded
- some backend/client contracts are stable but still evolving
- analyze+generate sync endpoint still exists for compatibility and may eventually be deprecated

---

## Last Major Changes

Recent implemented changes:
- queue-based backend generation jobs
- progress reporting from diffusers steps
- cancel support for jobs
- analyze+generate background job endpoint
- runtime info endpoint
- runtime info shown in client
- cancel button added to status bar
- `MainViewModel` split into partial files
- theme service foundation started

---

## Handoff Note

If this project is continued in a new chat, the next recommended focus is:

1. complete theme system and settings
2. finish refactoring `MainViewModel`
3. document backend/client architecture and API contract
4. improve backend cache/job persistence