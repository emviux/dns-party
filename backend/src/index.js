export default {
  async fetch(request, env, ctx) {
    // 1. CORS Preflight
    if (request.method === "OPTIONS") {
      return new Response(null, {
        headers: {
          "Access-Control-Allow-Origin": "*",
          "Access-Control-Allow-Methods": "POST, OPTIONS",
          "Access-Control-Allow-Headers": "Content-Type",
          "Access-Control-Max-Age": "86400",
        },
      });
    }

    if (request.method !== "POST") {
      return new Response("Method not allowed", { status: 405 });
    }

    try {
      // 2. Rate limiting check (basic: no body parsing yet)
      // A full rate limit would use durable objects, KV, or Cloudflare rate limiting rules.
      // Here we trust Cloudflare's basic DDoS protection, but we can do a quick check.

      // 3. Parse JSON
      const body = await request.json();
      
      // 4. Validate body
      const { category, message, appVersion, os, referenceId } = body;
      
      if (!message || message.trim().length === 0) {
        return new Response(JSON.stringify({ error: "Message is required." }), { 
          status: 400,
          headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
        });
      }

      if (message.length > 5000) {
        return new Response(JSON.stringify({ error: "Message exceeds 5000 characters." }), { 
          status: 400,
          headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
        });
      }

      const validCategories = ["Bug Report", "Feature Suggestion", "General Feedback"];
      if (!validCategories.includes(category)) {
        return new Response(JSON.stringify({ error: "Invalid category." }), { 
          status: 400,
          headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
        });
      }

      // 5. GitHub API Integration
      if (!env.GITHUB_TOKEN) {
        return new Response(JSON.stringify({ error: "Server configuration error: GITHUB_TOKEN missing." }), { 
          status: 500,
          headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
        });
      }

      const repo = env.GITHUB_REPO || "emviux/dns-party";
      const githubUrl = `https://api.github.com/repos/${repo}/issues`;

      let titlePrefix = "[Feedback]";
      let label = "feedback";
      
      if (category === "Bug Report") {
        titlePrefix = "[Bug Report]";
        label = "bug";
      } else if (category === "Feature Suggestion") {
        titlePrefix = "[Feature Request]";
        label = "enhancement";
      }

      // Shorten the message for the title
      const shortDesc = message.length > 50 ? message.substring(0, 50) + "..." : message;
      const title = `${titlePrefix} ${shortDesc.replace(/\n/g, ' ')}`;

      const issueBody = `### User Message\n${message}\n\n### Application Information\n- Application: DNS Party\n- Application version: ${appVersion || "Unknown"}\n- Operating system: ${os || "Unknown"}\n- Submission timestamp (UTC): ${new Date().toISOString()}\n- Anonymous submission reference: ${referenceId || "Unknown"}\n\n### Category\n${category}`;

      const githubPayload = {
        title: title,
        body: issueBody,
        labels: [label]
      };

      const githubRequest = await fetch(githubUrl, {
        method: "POST",
        headers: {
          "Authorization": `Bearer ${env.GITHUB_TOKEN}`,
          "Accept": "application/vnd.github.v3+json",
          "User-Agent": "DNS-Party-Feedback-Worker"
        },
        body: JSON.stringify(githubPayload)
      });

      if (!githubRequest.ok) {
        const errorText = await githubRequest.text();
        console.error("GitHub API Error:", errorText);
        return new Response(JSON.stringify({ error: "Failed to create issue on GitHub." }), { 
          status: 502,
          headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
        });
      }

      const githubResponse = await githubRequest.json();
      
      return new Response(JSON.stringify({ 
        success: true, 
        issueNumber: githubResponse.number, 
        issueUrl: githubResponse.html_url 
      }), {
        headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
      });

    } catch (err) {
      console.error("Worker Error:", err);
      return new Response(JSON.stringify({ error: "An internal server error occurred." }), { 
        status: 500,
        headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
      });
    }
  },
};
