# Sky Pattern Hunter — Living Architecture Document
Version: 0.1.0  
Status: Draft  
Maintainer: Ryan W.

This document evolves with the system. Every architectural decision, change, and rationale is recorded here.

---

# 1. System Overview

Sky Pattern Hunter is a modular aircraft-tracking intelligence system built using Clean Architecture. It ingests ADS‑B signals, processes aircraft telemetry, applies ML.NET models, and triggers notifications.

The system is designed to run continuously with low CPU usage and high extensibility.

---

# 2. Architecture Goals

- High testability  
- Clean separation of concerns  
- Asynchronous APIs  
- Extendable to new antennas and data sources  
- Minimal external dependencies  
- Predictive analytics using ML.NET  
- Real-time event detection  

---

# 3. Architecture Layers

## 3.1 Domain Layer
- Pure business logic  
- Aircraft models  
- Event models  
- ML prediction interfaces  
- No external dependencies  

## 3.2 Application Layer
- Use cases  
- Orchestrates workflows  
- Interfaces for infrastructure  

## 3.3 Infrastructure Layer
- ADS‑B ingestion  
- ML.NET pipelines  
- SMS provider  
- JSONL storage  
- Configuration loader  

## 3.4 Presentation Layer
- Windows UI  
- Real-time aircraft list  
- Event viewer  
- Settings editor  

---

# 4. Data Flow Diagram (Mermaid)

```mermaid
flowchart LR
    Pi[ADS-B Feed (Raspberry Pi)] --> Ingest[ADS-B Ingestion Service]
    Ingest --> Normalize[Normalization]
    Normalize --> Domain[Domain Models]
    Domain --> Detect[Event Detection]
    Detect --> ML[ML.NET Predictions]
    ML --> Events[Event Stream]
    Events --> Notify[SMS Notification Service]
    Events --> UI[Desktop UI]
    Events --> Storage[JSONL Storage]
