import type { APIRoute } from 'astro';

const API_URL = import.meta.env.API_URL || 'http://localhost:5000';

export const GET: APIRoute = async ({ params, locals }) => {
  const userId = locals.userId;
  const { orgId } = params;

  if (!userId || !orgId) {
    return new Response(JSON.stringify({ error: 'Unauthorized or missing orgId' }), {
      status: 401,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  try {
    const response = await fetch(`${API_URL}/api/orgs/${orgId}?userId=${userId}`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
    });

    if (!response.ok) {
      return new Response(JSON.stringify({ error: 'Failed to fetch org' }), {
        status: response.status,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    const org = await response.json();
    return new Response(JSON.stringify(org), {
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

export const PUT: APIRoute = async ({ params, locals, request }) => {
  const userId = locals.userId;
  const { orgId } = params;

  if (!userId || !orgId) {
    return new Response(JSON.stringify({ error: 'Unauthorized or missing orgId' }), {
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

    const response = await fetch(`${API_URL}/api/orgs/${orgId}?userId=${userId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name }),
    });

    if (!response.ok) {
      return new Response(JSON.stringify({ error: 'Failed to update org' }), {
        status: response.status,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    return new Response(null, { status: 204 });
  } catch (error) {
    return new Response(JSON.stringify({ error: 'Internal server error' }), {
      status: 500,
      headers: { 'Content-Type': 'application/json' },
    });
  }
};

export const DELETE: APIRoute = async ({ params, locals }) => {
  const userId = locals.userId;
  const { orgId } = params;

  if (!userId || !orgId) {
    return new Response(JSON.stringify({ error: 'Unauthorized or missing orgId' }), {
      status: 401,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  try {
    const response = await fetch(`${API_URL}/api/orgs/${orgId}?userId=${userId}`, {
      method: 'DELETE',
      headers: { 'Content-Type': 'application/json' },
    });

    if (!response.ok) {
      return new Response(JSON.stringify({ error: 'Failed to delete org' }), {
        status: response.status,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    return new Response(null, { status: 204 });
  } catch (error) {
    return new Response(JSON.stringify({ error: 'Internal server error' }), {
      status: 500,
      headers: { 'Content-Type': 'application/json' },
    });
  }
};