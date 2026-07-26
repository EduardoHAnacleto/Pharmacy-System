export interface Holiday {
  date: string
  localName: string
  name: string
}

/**
 * Public holidays for a country, from the Nager.Date open API.
 *
 * The country is a parameter rather than the `/BR` that used to be written into
 * the URL: a shop in New Zealand needs New Zealand's holidays, and its opening
 * hours are wrong on Waitangi Day otherwise.
 */
export async function getHolidays(year: number, countryCode: string): Promise<Holiday[]> {
  const country = countryCode.trim().toUpperCase()

  if (!/^[A-Z]{2}$/.test(country)) {
    throw new Error(`Código de país inválido: ${countryCode}`)
  }

  const response = await fetch(`https://date.nager.at/api/v3/PublicHolidays/${year}/${country}`)

  if (!response.ok) {
    throw new Error('Erro ao buscar feriados')
  }

  return response.json()
}
