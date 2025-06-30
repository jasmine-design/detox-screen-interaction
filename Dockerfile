# Use a lightweight Python base image
FROM python:3.10-slim

# Set environment variables
ENV DEBIAN_FRONTEND=noninteractive \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

# Install system-level dependencies
RUN apt-get update && apt-get install -y \
    ffmpeg \
    git \
    curl \
    espeak-ng \
    build-essential \
    libsndfile1 \
    && rm -rf /var/lib/apt/lists/*

# Set working directory inside the container
WORKDIR /app

# Copy dependency list
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy entire project into the container
COPY . .

# Expose the port used by Flask
EXPOSE 5002

# Run the Flask app
CMD ["python", "main.py"]