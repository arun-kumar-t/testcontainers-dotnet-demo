const http = require('http');
const url = require('url');

const PORT = 3000;
let deviceIdCounter = 1;

// POST /mock endpoint
const handleMock = (req, res) => {
    if (req.method === 'POST') {
        let body = '';
        req.on('data', chunk => {
            body += chunk.toString();
        });
        req.on('end', () => {
            const payload = {
                deviceId: `device-${deviceIdCounter++}`,
                temperature: (20 + Math.random() * 10).toFixed(2),
                humidity: (50 + Math.random() * 20).toFixed(2),
                timestamp: new Date().toISOString()
            };
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(payload));
        });
    } else {
        res.writeHead(405, { 'Content-Type': 'text/plain' });
        res.end('Method Not Allowed');
    }
};

// SSE /events endpoint
const handleEvents = (req, res) => {
    res.writeHead(200, {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        'Connection': 'keep-alive',
        'Access-Control-Allow-Origin': '*'
    });

    let eventCounter = 1;
    const interval = setInterval(() => {
        const payload = {
            deviceId: `device-sse-${eventCounter}`,
            temperature: (20 + Math.random() * 10).toFixed(2),
            humidity: (50 + Math.random() * 20).toFixed(2),
            timestamp: new Date().toISOString()
        };
        
        res.write(`data: ${JSON.stringify(payload)}\n\n`);
        eventCounter++;
    }, 1000);

    req.on('close', () => {
        clearInterval(interval);
    });
};

// Health check
const handleHealth = (req, res) => {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ status: 'healthy' }));
};

const server = http.createServer((req, res) => {
    const parsedUrl = url.parse(req.url, true);
    const path = parsedUrl.pathname;

    if (path === '/mock' && req.method === 'POST') {
        handleMock(req, res);
    } else if (path === '/events') {
        handleEvents(req, res);
    } else if (path === '/health') {
        handleHealth(req, res);
    } else {
        res.writeHead(404, { 'Content-Type': 'text/plain' });
        res.end('Not Found');
    }
});

server.listen(PORT, () => {
    console.log(`Fake device server running on port ${PORT}`);
});

