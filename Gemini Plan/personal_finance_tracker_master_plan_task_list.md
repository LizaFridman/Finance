# Local Personal Finance Tracker: Master Plan & Session Task List

This document serves as a self-contained blueprint for building a local, privacy-first personal finance tracking application using **Claude Code Pro**, **Python**, **Streamlit**, **SQLite**, **Pandas**, and **pdfplumber**.

Each section below is structured as an independent session task list with precise prompts you can feed directly into Claude Code.

---

## Technical Stack & Architecture
* **Core Language:** Python 3.10+
* **UI & Dashboard:** Streamlit (interactive web app, native charts, file uploader)
* **Database:** SQLite (local file database `finances.db`)
* **Parsing Engines:** `pdfplumber` (layout-aware PDF parsing), built-in XML/CSV libraries
* **Data Manipulation:** Pandas

---

## Session 1: Project Initialization & Database Schema

### Objective
Set up the project folder structure, Python virtual environment, dependencies, and the SQLite database with a robust schema.

### Tasks
1. Create project directory structure:
   ```text
   finance_app/
   ├── app.py
   ├── database.py
   ├── parsers/
   │   ├── __init__.py
   │   ├── pdf_parser.py
   │   └── xml_parser.py
   ├── utils.py
   ├── requirements.txt
   └── finances.db (generated)
   ```
2. Write `requirements.txt` containing:
   ```text
   streamlit>=1.30.0
   pandas>=2.0.0
   pdfplumber>=0.10.0
   plotly>=5.0.0
   ```
3. Implement `database.py` with tables for:
   * `transactions`: Unified records (`id`, `date`, `amount`, `merchant`, `category`, `statement_source`, `owner`, `is_shared`, `split_percentage`).
   * `statements`: Logs uploaded files to prevent duplicate batch processing (`id`, `filename`, `upload_date`, `hash`).
   * `split_rules`: Remembers category or merchant-based sharing rules (`id`, `pattern`, `default_split`).

### Prompt for Claude Code (Session 1)
> "Set up the initial project structure for a local Streamlit personal finance app. Create a `requirements.txt` file and a `database.py` module that initializes a SQLite database (`finances.db`) with three tables: `transactions`, `statements`, and `split_rules`. Ensure proper foreign key constraints and indexes on date and merchant."
