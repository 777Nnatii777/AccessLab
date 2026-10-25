type HealthResponse = {
    message: string
    numer: number
    version: string
}

export async function getHealth(): Promise<HealthResponse> {
    const response = await fetch('/api/health')

    if (!response.ok) {
        throw new Error(`Failed to fetch health: ${response.statusText}`)
    }

    return response.json()
}