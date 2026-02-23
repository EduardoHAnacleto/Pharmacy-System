export interface Holiday {
  date: string
  localName: string
  name: string
}

export async function getHolidays(year: number): Promise<Holiday[]> {
  const response = await fetch(`https://date.nager.at/api/v3/PublicHolidays/${year}/BR`)

  if (!response.ok) {
    throw new Error('Erro ao buscar feriados')
  }

  return response.json()
}
