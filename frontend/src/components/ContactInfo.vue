<template>
  <section class="py-5">
    <div class="container px-4 px-lg-5 mt-5">
      <div class="row justify-content-center">
        <div class="col-lg-8">
          <div class="card shadow-sm">
            <div class="card-body p-4">
              <h2 class="fw-bolder mb-4 text-center">{{ t('contact.title') }}</h2>

              <ul class="list-group list-group-flush">
                <!-- PHONE -->
                <li v-if="store.phone || store.whatsAppNumber" class="list-group-item">
                  <strong>{{ t('contact.phone') }}:</strong>

                  <a v-if="store.phone" :href="`tel:${telHref}`" class="text-decoration-none ms-2">
                    {{ store.phone }}
                  </a>

                  <a
                    v-if="whatsAppHref"
                    :href="whatsAppHref"
                    target="_blank"
                    rel="noopener"
                    class="text-decoration-none ms-2 d-inline-flex align-items-center"
                  >
                    <i class="bi bi-whatsapp text-success me-1" aria-hidden="true"></i>
                    {{ t('contact.whatsapp') }}
                  </a>
                </li>

                <!-- EMAIL -->
                <li v-if="store.email" class="list-group-item">
                  <strong>{{ t('contact.email') }}:</strong>
                  <a :href="mailHref" class="text-decoration-none ms-2">{{ store.email }}</a>
                </li>

                <!-- ADDRESS -->
                <li v-if="addressLine" class="list-group-item">
                  <strong>{{ t('contact.address') }}:</strong>
                  {{ addressLine }}
                  <a
                    v-if="mapSearchUrl"
                    :href="mapSearchUrl"
                    target="_blank"
                    rel="noopener"
                    class="text-decoration-none ms-2"
                  >
                    {{ t('contact.viewOnMap') }}
                  </a>
                </li>

                <!-- OPENING HOURS -->
                <li class="list-group-item">
                  <strong>{{ t('contact.openingHours') }}:</strong>

                  <span v-if="!settings.hasOpeningHours" class="ms-2 d-block text-muted">
                    {{ t('contact.noHours') }}
                  </span>

                  <span v-else class="ms-2 d-block">
                    <span v-for="day in week" :key="day.weekday" class="d-block">
                      {{ t(`weekday.${day.weekday}`) }}: {{ day.label }}
                    </span>
                  </span>
                </li>

                <!-- STATUS -->
                <li class="list-group-item">
                  <strong>{{ t('contact.status') }}:</strong>
                  <span class="ms-2 badge" :class="isOpen ? 'bg-success' : 'bg-danger'">
                    {{ isOpen ? t('contact.open') : t('contact.closed') }}
                  </span>
                  <span v-if="todayHoliday" class="ms-2 text-muted small">
                    {{
                      t('contact.holiday', { name: todayHoliday.localName || todayHoliday.name })
                    }}
                  </span>
                </li>
              </ul>

              <!-- MAP -->
              <div v-if="mapEmbedUrl" class="ratio ratio-16x9 mt-3">
                <iframe
                  :src="mapEmbedUrl"
                  :title="t('contact.address')"
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
import { computed, onMounted, onBeforeUnmount, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { getHolidays, type Holiday } from '@/services/holidayService'
import { useSettingsStore } from '@/stores/settings'

const { t } = useI18n()
const settings = useSettingsStore()

const store = computed(() => settings.settings)

const holidays = ref<Holiday[]>([])
const now = ref(new Date())

let ticker: ReturnType<typeof setInterval> | null = null

onMounted(async () => {
  // Country comes from settings, so a shop outside Brazil gets its own calendar
  // instead of the /BR that used to be hardcoded into the URL.
  try {
    holidays.value = await getHolidays(new Date().getFullYear(), store.value.countryCode)
  } catch {
    // Opening hours still work without the holiday calendar; a third-party
    // outage must not blank the contact page.
    holidays.value = []
  }

  ticker = setInterval(() => {
    now.value = new Date()
  }, 60000)
})

onBeforeUnmount(() => {
  if (ticker) clearInterval(ticker)
})

// ===== CONTACT LINKS =====

const telHref = computed(() => (store.value.phone ?? '').replace(/[^\d+]/g, ''))

const mailHref = computed(() => `mailto:${store.value.email}`)

const whatsAppHref = computed(() => {
  const number = settings.whatsAppNumber
  if (!number) return null

  return `https://wa.me/${number}?text=${encodeURIComponent(t('contact.whatsappGreeting'))}`
})

const addressLine = computed(
  () => [store.value.address, store.value.city].filter(Boolean).join(' - ') || null,
)

// ===== MAP =====

/**
 * Built from the shop's own address rather than a pasted embed URL.
 *
 * The original markup embedded a fixed URL — pointing at the Tower of Pisa — which
 * no second shop could change and which nobody could correct from the admin
 * screen. Deriving it from the address means it is right by construction.
 */
const mapEmbedUrl = computed(() => {
  const query = settings.mapQuery
  if (!query) return null

  return `https://www.google.com/maps?q=${encodeURIComponent(query)}&output=embed`
})

const mapSearchUrl = computed(() => {
  const query = settings.mapQuery
  if (!query) return null

  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(query)}`
})

// ===== OPENING HOURS =====

const todayISO = computed(() => {
  // Formatted in the shop's time zone: "today" for the holiday lookup has to be
  // the shop's today, not the visitor's.
  try {
    return new Intl.DateTimeFormat('en-CA', {
      timeZone: store.value.timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(now.value)
  } catch {
    return now.value.toISOString().slice(0, 10)
  }
})

const todayHoliday = computed(() => holidays.value.find((h) => h.date === todayISO.value))

const isOpen = computed(() => settings.isOpenAt(now.value, Boolean(todayHoliday.value)))

/** Monday-first week with each day's ranges rendered as a single string. */
const week = computed(() =>
  [1, 2, 3, 4, 5, 6, 7].map((weekday) => {
    const ranges = store.value.openingHours[String(weekday)] ?? []

    return {
      weekday,
      label:
        ranges.length > 0
          ? ranges.map((r) => `${r.open} – ${r.close}`).join(', ')
          : t('contact.closedAllDay'),
    }
  }),
)
</script>
