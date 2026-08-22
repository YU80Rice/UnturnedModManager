import { createServer } from "node:http";
import { createWriteStream, mkdirSync } from "node:fs";
import { dirname } from "node:path";

const outputPath = process.argv[2];
if (!outputPath) throw new Error("recording output path is required");
mkdirSync(dirname(outputPath), { recursive: true });

const server = createServer((request, response) => {
  const corsHeaders = {
    "Access-Control-Allow-Origin": "http://127.0.0.1:5174",
    "Access-Control-Allow-Methods": "POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type"
  };
  if (request.method === "OPTIONS") {
    response.writeHead(204, corsHeaders);
    response.end();
    return;
  }

  if (request.method !== "POST" || request.url !== "/upload") {
    response.writeHead(404, corsHeaders);
    response.end();
    return;
  }

  const stream = createWriteStream(outputPath, { flags: "w" });
  request.pipe(stream);
  request.on("error", (error) => stream.destroy(error));
  stream.on("finish", () => {
    response.writeHead(200, corsHeaders);
    response.end("saved");
    console.log(`recording saved: ${outputPath}`);
  });
});

server.listen(5175, "127.0.0.1", () => console.log("recording receiver ready on 5175"));
