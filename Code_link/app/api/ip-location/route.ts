import { NextRequest, NextResponse } from "next/server";

export async function GET(req: NextRequest) {
    const { searchParams } = new URL(req.url);
    const ip = searchParams.get("ip");

    if (!ip) {
        return NextResponse.json(
            { error: "Missing ip parameter" },
            { status: 400 }
        );
    }

    try {
        const response = await fetch(`http://ip-api.com/json/${ip}`);
        const data = await response.json();

        return NextResponse.json(data);
    } catch (error) {
        return NextResponse.json(
            { error: "Failed to fetch IP data" },
            { status: 500 }
        );
    }
}