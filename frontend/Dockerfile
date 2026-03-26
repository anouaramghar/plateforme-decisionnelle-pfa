# Étape de build avec Node.js
FROM node:18-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
# La commande dépend de votre outil (npm run build pour Vite/React)
RUN npm run build 

# Étape de production (Serveur web léger)
FROM nginx:alpine
# Si vous utilisez Vite, le dossier est souvent 'dist'. Si Create React App, c'est 'build'.
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]