<template>
  <section class="py-5">
    <div class="container px-4 px-lg-5 mt-5">
      <div class="row justify-content-center">
        <div class="col-lg-8">
          <div class="card shadow-sm">
            <div class="card-body p-4">
              <h2 class="fw-bolder mb-4 text-center">Contato</h2>

              <ul class="list-group list-group-flush">
                <li class="list-group-item">
                  <strong>Telefone:</strong>
                  <a href="tel:+641111111111" class="text-decoration-none ms-2">
                    (00) 12345-6789
                  </a>
                  <a href="tel:+641111111111" class="text-decoration-none ms-2">
                    Ou (00) 1234-6789
                  </a>
                  <a
                    href="https://wa.me/641111111111?text=Olá,%20gostaria%20de%20mais%20informações"
                    target="_blank"
                    class="text-decoration-none ms-2 d-inline-flex align-items-center"
                  >
                    <i class="bi bi-whatsapp text-success me-1"></i>
                    Falar no WhatsApp
                  </a>
                </li>

                <p class="list-group-item">
                  <strong>E-mail:</strong>
                  <a
                    href="mailto:EMAIL@MAIL.com?subject=Contato%20via%20Site"
                    class="text-decoration-none ms-2"
                  >
                    EMAIL@mail.com
                  </a>
                </p>

                <li class="list-group-item">
                  <strong>Endereço:</strong>
                  ADDRESS ADDRESS ADDRESS
                  <a
                    href="https://www.google.com/maps/search/?api=1&query=TOWER+OF+PISA"
                    target="_blank"
                    class="text-decoration-none ms-2"
                  >
                    (Ver no mapa)
                  </a>
                </li>
                <li class="list-group-item">
                  <strong>Horário de funcionamento:</strong>
                  <span class="ms-2 d-block">
                    Segunda a Sexta: 08:00 às 20:00<br />
                    Sábado: 08:00 às 14:00<br />
                    Domingo: Fechado
                  </span>
                </li>
                <p class="mt-3">
                  <strong>Status:</strong>
                  <span class="ms-2 badge" :class="isOpen ? 'bg-success' : 'bg-danger'">
                    {{ isOpen ? 'Aberto agora' : 'Fechado agora' }}
                  </span>
                </p>
              </ul>
              <p class="text-muted small">
                {{ todaySchedule }}
              </p>
              <div class="ratio ratio-16x9 mt-3">
                <iframe
                  src="https://www.google.com/maps?q=TOWER+OF+PISA&output=embed"
                  style="border: 0"
                  allowfullscreen
                  loading="lazy"
                  referrerpolicy="no-referrer-when-downgrade"
                >
                </iframe>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { getHolidays } from '@/services/holidayService'

interface Holiday {
  date: string
  name: string
}

const holidays = ref<Holiday[]>([])
const now = ref(new Date())

const openingRules = {
  weekday: { start: 8, end: 18 },
  saturday: { start: 8, end: 12 },
  holiday: { start: 9, end: 13 },
}

function getCompanyDate(): Date {
  const nowUTC = new Date()
  const offsetMinutes = nowUTC.getTimezoneOffset()
  const saoPauloOffset = 180
  return new Date(nowUTC.getTime() + (offsetMinutes - saoPauloOffset) * 60000)
}

onMounted(async () => {
  holidays.value = await getHolidays(new Date().getFullYear())

  setInterval(() => {
    now.value = getCompanyDate()
  }, 60000)
})

const todayISO = computed(() => {
  const d = now.value
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(
    d.getDate(),
  ).padStart(2, '0')}`
})

const todayHoliday = computed(() => holidays.value.find((h) => h.date === todayISO.value))

const isOpen = computed(() => {
  const hour = now.value.getHours()
  const day = now.value.getDay()

  if (todayHoliday.value) {
    return hour >= openingRules.holiday.start && hour < openingRules.holiday.end
  }

  if (day >= 1 && day <= 5) {
    return hour >= openingRules.weekday.start && hour < openingRules.weekday.end
  }

  if (day === 6) {
    return hour >= openingRules.saturday.start && hour < openingRules.saturday.end
  }

  return false
})

const todaySchedule = computed(() => {
  if (todayHoliday.value) {
    return `Feriado (${todayHoliday.value.name}): 09:00 às 13:00`
  }

  const day = now.value.getDay()
  if (day === 6) return 'Sábado: 08:00 às 12:00'
  if (day === 0) return 'Domingo: fechado'

  return 'Seg–Sex: 08:00 às 18:00'
})
</script>
