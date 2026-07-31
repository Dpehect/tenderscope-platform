import { NextRequest, NextResponse } from "next/server";

function unauthorized() {
  return new NextResponse("Authentication required", {
    status: 401,
    headers: {
      "WWW-Authenticate": 'Basic realm="TenderScope Operations", charset="UTF-8"',
      "Cache-Control": "no-store"
    }
  });
}

export function proxy(request: NextRequest) {
  const password = process.env.ADMIN_DASHBOARD_PASSWORD;
  const username = process.env.ADMIN_DASHBOARD_USERNAME ?? "admin";

  if (!password) {
    return new NextResponse("Not found", {
      status: 404,
      headers: { "Cache-Control": "no-store" }
    });
  }

  const authorization = request.headers.get("authorization");
  if (!authorization?.startsWith("Basic ")) return unauthorized();

  try {
    const decoded = atob(authorization.slice(6));
    const separator = decoded.indexOf(":");
    const suppliedUsername = decoded.slice(0, separator);
    const suppliedPassword = decoded.slice(separator + 1);

    if (separator < 0 || suppliedUsername !== username || suppliedPassword !== password) {
      return unauthorized();
    }
  } catch {
    return unauthorized();
  }

  const response = NextResponse.next();
  response.headers.set("X-Robots-Tag", "noindex, nofollow, noarchive");
  response.headers.set("Cache-Control", "private, no-store");
  return response;
}

export const config = {
  matcher: ["/admin/:path*"]
};
