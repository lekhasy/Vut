import type { APIRoute } from 'astro';

const API_URL = import.meta.env.API_URL || 'http://localhost:5000';

export const POST: APIRoute = async ({ params, locals, request }) => {
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
    const { email, role } = body;

    if (!email || typeof email !== 'string' || !role) {
      return new Response(JSON.stringify({ error: 'Invalid request body' }), {
        status: 400,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    const response = await fetch(
      `${API_URL}/api/orgs/${orgId}/invitations?inviterUserId=${userId}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, role }),
      },
    );

    if (!response.ok) {
      return new Response(JSON.stringify({ error: 'Failed to send invitation' }), {
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
