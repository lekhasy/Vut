import type { APIRoute } from 'astro';

const API_URL = import.meta.env.API_URL || 'http://localhost:5000';

export const GET: APIRoute = async ({ locals, cookies }) => {
  const userId = locals.userId;

  if (!userId) {
    return new Response(JSON.stringify({ error: 'Unauthorized' }), {
      status: 401,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  try {
    const response = await fetch(`${API_URL}/api/orgs?userId=${userId}`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
    });

    if (!response.ok) {
      return new Response(JSON.stringify({ error: 'Failed to fetch orgs' }), {
        status: response.status,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    const orgs = await response.json();
    return new Response(JSON.stringify(orgs), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });
  } catch (error) {
    return new Response(JSON.stringify({ error: 'Internal server error' }), {
      status: 500,
      headers: { 'Content-Type': 'application/json' },
    });
  }
};

export const POST: APIRoute = async ({ locals, request }) => {
  const userId = locals.userId;

  if (!userId) {
    return new Response(JSON.stringify({ error: 'Unauthorized' }), {
      status: 401,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  try {
    const body = await request.json();
    const { name } = body;

    if (!name || typeof name !== 'string') {
      return new Response(JSON.stringify({ error: 'Invalid request body' }), {
        status: 400,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    const orgId = crypto.randomUUID();
    const response = await fetch(`${API_URL}/api/orgs?userId=${userId}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ orgId, name }),
    });

    if (!response.ok) {
      return new Response(JSON.stringify({ error: 'Failed to create org' }), {
        status: response.status,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    const org = await response.json();
    return new Response(JSON.stringify(org), {
      status: 201,
      headers: { 'Content-Type': 'application/json' },
    });
  } catch (error) {
    return new Response(JSON.stringify({ error: 'Internal server error' }), {
      status: 500,
      headers: { 'Content-Type': 'application/json' },
    });
  }
};
