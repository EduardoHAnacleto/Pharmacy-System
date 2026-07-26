# =====================
# BUILD
# =====================
FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

# No build-time configuration. Everything shop-specific — including the WhatsApp
# number — is fetched from /api/v1/store-settings at runtime, so one image serves
# every shop and changing a number needs no rebuild.
RUN npm run build

# =====================
# RUNTIME
# =====================
FROM nginx:alpine AS runtime

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
