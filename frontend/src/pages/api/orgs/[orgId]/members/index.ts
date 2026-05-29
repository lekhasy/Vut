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
    const response = await fetch(`${API_URL}/api/orgs/${orgId}/members?userId=${userId}`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
    });

    if (!response.ok) {
      return new Response(JSON.stringify({ error: 'Failed to fetch members' }), {
        status: response.status,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    const members = await response.json();
    return new Response(JSON.stringify(members), {
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