## Session 2: Modular Statement Ingestion Engine (PDF & XML)

### Objective
Build a flexible parsing engine using an adapter pattern to handle diverse bank and credit card statement formats (PDFs and XMLs) and map them into our unified transaction schema.

### Tasks
1. Implement `parsers/pdf_parser.py` using `pdfplumber` to extract structured text/tables from different credit card and bank statement layouts.
2. Implement `parsers/xml_parser.py` to parse XML-based bank exports.
3. Build a factory or manager pattern in `parsers/__init__.py` that auto-detects file types and routes them to the correct parser based on file extension and format structure.
4. Add a fallback extraction text parser for messy PDFs where standard table extraction fails.

### Prompt for Claude Code (Session 2)
> "Build a modular parsing backend for our finance app inside a `parsers/` folder. Create a base adapter class, along with `pdf_parser.py` (using `pdfplumber` to handle layout-aware table/text extraction) and `xml_parser.py`. Implement an auto-detection dispatcher that maps disparate column names (such as date, amount, merchant in English or Hebrew/other local formats) into our unified database transaction dictionary structure."