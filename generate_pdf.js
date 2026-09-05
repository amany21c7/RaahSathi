const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

async function main() {
    console.log("1. Reading markdown file...");
    const mdPath = path.join(__dirname, "RaahSathi_Database_Documentation.md");
    const htmlSnippet = execSync(`npx -y marked -i "${mdPath}" --gfm`).toString();

    console.log("2. Generating full styled HTML document...");
    const fullHtml = `<!DOCTYPE html>
<html lang="hi">
<head>
    <meta charset="UTF-8">
    <title>RaahSathi Database & Stored Procedures Technical Reference Manual</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@500;600;700;800&family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;600&display=swap" rel="stylesheet">
    <style>
        @page {
            size: A4 portrait;
            margin: 14mm 14mm 16mm 14mm;
            @bottom-right {
                content: "Page " counter(page);
                font-family: 'Inter', sans-serif;
                font-size: 9pt;
                color: #64748b;
            }
        }
        
        * {
            box-sizing: border-box;
        }

        body {
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
            color: #1e293b;
            line-height: 1.55;
            font-size: 9.5pt;
            background: #ffffff;
            margin: 0;
            padding: 0;
            -webkit-print-color-adjust: exact !important;
            print-color-adjust: exact !important;
        }

        /* Top Header Banner */
        .doc-header {
            border-bottom: 3px solid #ff6b00;
            padding-bottom: 12px;
            margin-bottom: 20px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .brand-title {
            font-family: 'Outfit', sans-serif;
            font-size: 26pt;
            font-weight: 800;
            color: #0f172a;
            letter-spacing: -0.5px;
            margin: 0;
        }
        .brand-orange { color: #ff6b00; }
        .doc-badge {
            background: #ff6b00;
            color: #ffffff;
            font-size: 8.5pt;
            font-weight: 700;
            padding: 4px 10px;
            border-radius: 6px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            display: inline-block;
        }

        /* Headings */
        h1 {
            font-family: 'Outfit', sans-serif;
            font-size: 19pt;
            font-weight: 800;
            color: #0f172a;
            margin-top: 26px;
            margin-bottom: 12px;
            padding-bottom: 6px;
            border-bottom: 2px solid #e2e8f0;
            page-break-after: avoid;
            break-after: avoid;
        }
        h2 {
            font-family: 'Outfit', sans-serif;
            font-size: 14pt;
            font-weight: 700;
            color: #0f172a;
            margin-top: 22px;
            margin-bottom: 10px;
            padding-bottom: 4px;
            border-bottom: 1.5px solid #ff6b00;
            page-break-after: avoid;
            break-after: avoid;
        }
        h3 {
            font-family: 'Outfit', sans-serif;
            font-size: 11.5pt;
            font-weight: 700;
            color: #1e3a8a;
            margin-top: 16px;
            margin-bottom: 8px;
            page-break-after: avoid;
            break-after: avoid;
        }
        h4 {
            font-family: 'Outfit', sans-serif;
            font-size: 10pt;
            font-weight: 700;
            color: #334155;
            margin-top: 12px;
            margin-bottom: 6px;
            page-break-after: avoid;
            break-after: avoid;
        }

        p, ul, ol {
            margin-top: 0;
            margin-bottom: 8px;
        }

        li {
            margin-bottom: 4px;
        }

        /* Blockquote Callouts */
        blockquote {
            background: #fff7ed;
            border-left: 4px solid #ff6b00;
            margin: 12px 0;
            padding: 10px 14px;
            border-radius: 0 6px 6px 0;
            color: #7c2d12;
            font-size: 9.5pt;
        }
        blockquote p {
            margin: 0;
        }

        /* Tables */
        table {
            width: 100%;
            border-collapse: collapse;
            margin: 10px 0 16px 0;
            font-size: 8.5pt;
            page-break-inside: auto;
        }
        tr {
            page-break-inside: avoid;
            page-break-after: auto;
        }
        thead {
            display: table-header-group;
        }
        th {
            background-color: #0f172a;
            color: #ffffff;
            font-family: 'Outfit', sans-serif;
            font-weight: 600;
            text-align: left;
            padding: 7px 9px;
            border: 1px solid #1e293b;
            font-size: 8.5pt;
            letter-spacing: 0.2px;
        }
        td {
            padding: 6px 9px;
            border: 1px solid #cbd5e1;
            vertical-align: top;
        }
        tbody tr:nth-child(even) {
            background-color: #f8fafc;
        }
        tbody tr:hover {
            background-color: #f1f5f9;
        }

        /* Code & Preformatted */
        code {
            font-family: 'JetBrains Mono', Consolas, Monaco, monospace;
            background-color: #f1f5f9;
            color: #0f172a;
            padding: 2px 5px;
            border-radius: 4px;
            font-size: 8pt;
            border: 1px solid #e2e8f0;
        }
        pre {
            background-color: #090d16;
            color: #f8fafc;
            padding: 10px 14px;
            border-radius: 6px;
            overflow-x: auto;
            font-size: 8pt;
            line-height: 1.45;
            margin: 10px 0 14px 0;
            border: 1px solid #1e293b;
            page-break-inside: avoid;
        }
        pre code {
            background: none;
            color: inherit;
            padding: 0;
            border: none;
            font-size: inherit;
        }

        hr {
            border: none;
            border-top: 1px solid #e2e8f0;
            margin: 18px 0;
        }

        /* Key badges & Links */
        a {
            color: #0284c7;
            text-decoration: none;
        }
        a:hover {
            text-decoration: underline;
        }

        .footer-note {
            margin-top: 30px;
            padding-top: 12px;
            border-top: 2px solid #e2e8f0;
            text-align: center;
            font-size: 8pt;
            color: #64748b;
        }
    </style>
</head>
<body>
    <div class="doc-header">
        <div>
            <div class="brand-title">Raah<span class="brand-orange">Sathi</span></div>
            <div style="font-size: 10pt; color: #64748b; font-weight: 500;">Enterprise Roadside Assistance Platform</div>
        </div>
        <div style="text-align: right;">
            <div class="doc-badge">Database Architecture & SP Manual</div>
            <div style="font-size: 8.5pt; color: #64748b; margin-top: 4px;">DB: Microsoft SQL Server (RaahSathiDb)</div>
        </div>
    </div>

    ${htmlSnippet}

    <div class="footer-note">
        RaahSathi Technical Architecture Documentation | Confidential & Enterprise Grade | Generated for System Operations
    </div>
</body>
</html>`;

    const htmlPath = path.join(__dirname, "RaahSathi_Database_Documentation.html");
    fs.writeFileSync(htmlPath, fullHtml, "utf8");
    console.log("3. Saved HTML to:", htmlPath);

    console.log("4. Rendering PDF via Headless Chrome / Edge...");
    const pdfPath = path.join(__dirname, "RaahSathi_Database_Documentation.pdf");

    // Path to Chrome or Edge
    let browserExe = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
    if (!fs.existsSync(browserExe)) {
        browserExe = "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";
    }

    const command = `"${browserExe}" --headless=new --disable-gpu --no-pdf-header-footer --print-to-pdf="${pdfPath}" "${htmlPath}"`;
    console.log("Running command:", command);
    execSync(command, { stdio: "inherit" });

    if (fs.existsSync(pdfPath)) {
        const stats = fs.statSync(pdfPath);
        console.log(`✅ SUCCESS! PDF created successfully: ${pdfPath} (${(stats.size / 1024).toFixed(1)} KB)`);
    } else {
        console.error("❌ Failed to create PDF.");
    }
}

main().catch(err => {
    console.error("Error generating PDF:", err);
    process.exit(1);
});
