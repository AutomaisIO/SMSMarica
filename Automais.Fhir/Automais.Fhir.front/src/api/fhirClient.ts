// Cliente mínimo da API FHIR (Automais.Fhir.Api). Conteúdo trafega como
// application/fhir+json; erros chegam como OperationOutcome.

const BASE = import.meta.env.VITE_FHIR_API_BASE_URL ?? '/fhir-api'

const FHIR_MEDIA_TYPE = 'application/fhir+json'

/** Recurso FHIR genérico (tipagem fina virá com os modelos por recurso). */
export interface FhirResource {
  resourceType: string
  id?: string
  [key: string]: unknown
}

export interface FhirBundle extends FhirResource {
  resourceType: 'Bundle'
  total?: number
  entry?: Array<{ resource: FhirResource }>
}

export class FhirError extends Error {
  constructor(
    public readonly status: number,
    public readonly outcome: FhirResource | null,
  ) {
    super(`FHIR ${status}`)
    this.name = 'FhirError'
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      Accept: FHIR_MEDIA_TYPE,
      ...(init?.body ? { 'Content-Type': FHIR_MEDIA_TYPE } : {}),
      ...init?.headers,
    },
  })

  const text = await res.text()
  const body = text ? JSON.parse(text) : null

  if (!res.ok) {
    // body é um OperationOutcome
    throw new FhirError(res.status, body)
  }
  return body as T
}

export const PatientApi = {
  read: (id: string) => request<FhirResource>(`/fhir/Patient/${id}`),
  search: (params: { identifier?: string; name?: string }) => {
    const qs = new URLSearchParams(
      Object.entries(params).filter(([, v]) => v) as [string, string][],
    ).toString()
    return request<FhirBundle>(`/fhir/Patient${qs ? `?${qs}` : ''}`)
  },
  create: (patient: FhirResource) =>
    request<FhirResource>('/fhir/Patient', { method: 'POST', body: JSON.stringify(patient) }),
}
