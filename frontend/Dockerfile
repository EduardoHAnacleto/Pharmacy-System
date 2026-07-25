# =====================
# BUILD
# =====================
FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

# Build-time configuration. Vite inlines VITE_* variables into the bundle,
# so these must be declared in this stage — setting them on the nginx stage
# happens after the build and never reaches the generated JavaScript.
ARG VITE_WHATSAPP_NUMBER
ENV VITE_WHATSAPP_NUMBER=$VITE_WHATSAPP_NUMBER

RUN npm run build

# =====================
# RUNTIME
# =====================
FROM nginx:alpine

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
