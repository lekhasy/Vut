import type { APIRoute } from 'astro';

const API_URL = import.meta.env.API_URL || 'http://localhost:5000';

export const DELETE: APIRoute = async ({ params, locals, request }) => {
  const userId = locals.userId;
  const { orgId, userId: targetUserId } = params;

  if (!userId || !orgId || !targetUserId) {
    return new Response(JSON.stringify({ error: 'Unauthorized or missing parameters' }), {
      status: 401,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  try {
    const response = await fetch(
      `${API_URL}/api/orgs/${orgId}/members/${targetUserId}?requesterUserId=${userId}`,
      {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
      },
    );

    if (!response.ok) {
      return new Response(JSON.stringify({ error: 'Failed to remove member' }), {
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
