/** True when the API refused a request because that feature is switched off for this institution (RequireFeatureAttribute's 403). Not a real failure: the section should simply not exist. */
export function isFeatureDisabledError(error: unknown): boolean {
  const response = (error as { response?: { status?: number; data?: { message?: string } } } | null)?.response;
  return response?.status === 403 && /feature is not enabled/i.test(response.data?.message ?? "");
}
